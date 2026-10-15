using Grove.Security.Antivirus.ClamAV;
using Grove.Media.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Grove.Media.Scanning.Scanners;

/// <summary>
/// ClamAV as an upload scanner (requirement 95): the Antivirus module's daemon connection
/// (INSTREAM over its socket), turned from a synchronous rejection into a verdict. A daemon that
/// cannot be reached, times out or answers with an error is a failure, never a pass.
/// </summary>
internal sealed class ClamAvUploadScanner : IUploadScanner
{
    public const string ScannerName = "ClamAV";

    private readonly ClamAvOptions _options;
    private readonly ClamAvConnectionFactory _connectionFactory;
    private readonly ILogger _logger;

    public ClamAvUploadScanner(IOptions<ClamAvOptions> options, ClamAvConnectionFactory connectionFactory, ILogger<ClamAvUploadScanner> logger)
    {
        _options = options.Value;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public string Name => ScannerName;

    public async Task<ScanVerdict> ScanAsync(ScanRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Validate(_options) is { } problem)
        {
            return ScanVerdict.Failed(problem);
        }

        try
        {
            await using var stream = await request.OpenAsync(cancellationToken);
            var response = await _connectionFactory.Create(_options).ScanAsync(stream, cancellationToken);

            return ParseResponse(response);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception, "ClamAV could not scan '{ScanId}'.", request.ScanId);

            return ScanVerdict.Failed($"ClamAV could not scan the file: {exception.Message}");
        }
    }

    /// <summary>clamd's INSTREAM answer: <c>stream: OK</c>, <c>stream: {signature} FOUND</c>, or an error.</summary>
    public static ScanVerdict ParseResponse(string? response)
    {
        response = response?.TrimEnd('\0', '\r', '\n', ' ');

        if (string.Equals(response, "stream: OK", StringComparison.Ordinal))
        {
            return ScanVerdict.Clean;
        }

        if (response is not null && response.EndsWith(" FOUND", StringComparison.Ordinal))
        {
            var signature = response[..^" FOUND".Length];
            var separator = signature.IndexOf(": ", StringComparison.Ordinal);

            if (separator >= 0)
            {
                signature = signature[(separator + 2)..];
            }

            return ScanVerdict.Rejected($"Malware detected by ClamAV: {signature}.");
        }

        return ScanVerdict.Failed($"ClamAV answered unexpectedly: {response}");
    }

    private static string? Validate(ClamAvOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
        {
            return "ClamAV is configured as a scanner but its host is not set.";
        }

        if (options.Port is < 1 or > 65535)
        {
            return "ClamAV is configured as a scanner but its port is invalid.";
        }

        if (options.ConnectTimeoutSeconds <= 0 || options.TransferTimeoutSeconds <= 0)
        {
            return "ClamAV is configured as a scanner but its timeouts must be greater than zero.";
        }

        return null;
    }
}
