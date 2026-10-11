using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Crest.Environment.Shell;
using Crest.Media.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crest.Media.Scanning.Scanners;

/// <summary>
/// A scanner reachable through an API (requirement 94): a scanning server beside the application,
/// or an external service, the host's or a tenant's own. Crest posts a signed job; the provider
/// fetches the quarantined file by its time-limited reference (or gets the bytes in the request
/// when the storage cannot make one) and answers at once (200 with a verdict) or later (202),
/// calling back on the verdict endpoint with a signed verdict.
/// </summary>
public sealed class OffsiteUploadScanner : IUploadScanner
{
    public const string HttpClientName = "UploadScanning";
    public const string JobHeader = "X-Scan-Job";

    private static readonly JsonSerializerOptions s_json = new(JsonSerializerDefaults.Web);

    private readonly OffsiteScannerOptions _scanner;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly UploadScanningOptions _options;
    private readonly ShellSettings _shellSettings;
    private readonly ILogger _logger;

    public OffsiteUploadScanner(
        string name,
        OffsiteScannerOptions scanner,
        IHttpClientFactory httpClientFactory,
        IOptions<UploadScanningOptions> options,
        ShellSettings shellSettings,
        ILogger<OffsiteUploadScanner> logger)
    {
        Name = name;
        _scanner = scanner;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _shellSettings = shellSettings;
        _logger = logger;
    }

    public string Name { get; }

    public async Task<ScanVerdict> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Uri.TryCreate(_scanner.Url, UriKind.Absolute, out var endpoint) || string.IsNullOrEmpty(_scanner.Secret))
        {
            return ScanVerdict.Failed($"The offsite scanner '{Name}' needs an absolute URL and a shared secret.");
        }

        var job = new OffsiteScanJob(request.ScanId, Name, request.Tenant, request.Name, request.ClaimedType, request.Size, request.TemporaryUri?.AbsoluteUri, CallbackUrl());
        var jobJson = JsonSerializer.SerializeToUtf8Bytes(job, s_json);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Add(ScanSignature.TimestampHeader, timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        message.Headers.Add(ScanSignature.SignatureHeader, ScanSignature.Sign(_scanner.Secret, timestamp, jobJson));

        Stream? bytes = null;
        try
        {
            if (request.TemporaryUri is not null)
            {
                message.Content = new ByteArrayContent(jobJson);
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
            else
            {
                // No reference to fetch by: the bytes go in the body, the signed job in a header.
                bytes = await request.OpenAsync(cancellationToken);
                message.Headers.Add(JobHeader, Convert.ToBase64String(jobJson));
                message.Content = new StreamContent(bytes);
                message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            }

            using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(message, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                return ScanVerdict.Pending;
            }

            if (!response.IsSuccessStatusCode)
            {
                return ScanVerdict.Failed($"The offsite scanner '{Name}' answered {(int)response.StatusCode}.");
            }

            var verdict = await response.Content.ReadFromJsonAsync<VerdictPayload>(s_json, cancellationToken);

            return verdict?.ToVerdict() ?? ScanVerdict.Failed($"The offsite scanner '{Name}' answered without a verdict.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or IOException || exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "The offsite scanner '{Scanner}' could not take scan '{ScanId}'.", Name, request.ScanId);

            return ScanVerdict.Failed($"The offsite scanner '{Name}' could not be reached: {exception.Message}");
        }
        finally
        {
            if (bytes is not null)
            {
                await bytes.DisposeAsync();
            }
        }
    }

    /// <summary>Where the provider reports back: the configured base URL, or the tenant's first host, plus its prefix.</summary>
    internal string? CallbackUrl()
    {
        var baseUrl = _options.CallbackBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var host = _shellSettings.RequestUrlHosts.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (host is null)
            {
                return null;
            }

            baseUrl = "https://" + host;
        }

        var prefix = _shellSettings.RequestUrlPrefix;

        return baseUrl.TrimEnd('/') + (string.IsNullOrEmpty(prefix) ? string.Empty : "/" + prefix) + "/" + Endpoints.VerdictCallbackEndpoint.Route;
    }

    internal sealed record OffsiteScanJob(string ScanId, string Scanner, string Tenant, string Name, string? ClaimedType, long Size, string? FileUrl, string? CallbackUrl);
}

/// <summary>A verdict as a provider sends it: <c>clean</c>, <c>rejected</c> with a reason, <c>failed</c>, or <c>pending</c>.</summary>
public sealed class VerdictPayload
{
    public string? ScanId { get; set; }

    public string? Scanner { get; set; }

    public string? Outcome { get; set; }

    public string? Reason { get; set; }

    public string? DetectedType { get; set; }

    public ScanVerdict? ToVerdict()
    {
        if (!Enum.TryParse<ScanOutcome>(Outcome, ignoreCase: true, out var outcome) || !Enum.IsDefined(outcome))
        {
            return null;
        }

        return outcome == ScanOutcome.Rejected && string.IsNullOrWhiteSpace(Reason)
            ? new ScanVerdict(outcome, "Rejected by the scanner.", DetectedType)
            : new ScanVerdict(outcome, Reason, DetectedType);
    }
}
