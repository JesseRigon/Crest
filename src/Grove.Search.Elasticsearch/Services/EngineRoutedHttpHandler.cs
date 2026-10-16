#nullable enable

using System.Net;
using System.Net.Http.Headers;
using Grove.Access;
using Grove.Data.Connectors;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Search.Elasticsearch.Services;

/// <summary>
/// The HTTP handler a long-lived vendor SDK client sends through: every request is one
/// <see cref="IConnectorEngine.RunSdkAsync{T}"/> call on the connection, as the tenant's system
/// actor, so the connection's rate limit, quota, retries (with <c>Retry-After</c>) and response
/// cap apply, and the request goes out on the engine's guarded client (address guard, no
/// redirects). The SDK's own retries are turned off where the client is built, so attempts never
/// multiply. A final HTTP answer, success or not, is handed back to the SDK as it came.
/// </summary>
internal sealed class EngineRoutedHttpHandler(IServiceProvider services, string connectionKey) : HttpMessageHandler
{
    public string ConnectionKey { get; } = connectionKey;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentHeaders = request.Content?.Headers.ToArray() ?? [];

        await using var scope = services.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetService<IConnectorEngine>()
            ?? throw new HttpRequestException($"Connection '{ConnectionKey}' needs the connector engine, which is not enabled.");
        var callers = scope.ServiceProvider.GetRequiredService<ICallerContextFactory>();
        var caller = await callers.CreateSystemAsync(cancellationToken: cancellationToken);

        ResponseSnapshot? last = null;
        var result = await engine.RunSdkAsync(
            ConnectionKey,
            caller,
            async (context, token) =>
            {
                using var attempt = Clone(request, body, contentHeaders);
                using var response = await context.HttpClient.SendAsync(attempt, HttpCompletionOption.ResponseHeadersRead, token);
                var snapshot = await ResponseSnapshot.ReadAsync(response, context.Connection.Policy.MaxResponseBytes, token);
                last = snapshot;

                // A retryable answer goes back to the engine's policy; any other is final.
                return IsRetryable(snapshot.StatusCode)
                    ? throw new RetryableStatusException(snapshot)
                    : snapshot;
            },
            new SdkCallOptions
            {
                Scope = CredentialScope.Tenant,
                IsSafeToRetry = IsSafe(request.Method),
                Classify = Classify,
            },
            cancellationToken);

        if (result.Succeeded && result.Result is { } final)
        {
            return final.ToResponse(request);
        }

        // Retries exhausted on an HTTP answer: the SDK sees that answer.
        if (result.Outcome == ConnectorOutcome.Failed && last is not null)
        {
            return last.ToResponse(request);
        }

        throw new HttpRequestException(
            $"Connection '{ConnectionKey}' {result.Outcome switch
            {
                ConnectorOutcome.RetryAfter => $"asked to wait until {result.RetryAt:u}",
                ConnectorOutcome.Refused => "refused the request",
                ConnectorOutcome.NotAuthorized => "has no usable credential",
                _ => "failed",
            }}: {result.Error}",
            null,
            result.StatusCode > 0 ? (HttpStatusCode)result.StatusCode : null);
    }

    private static bool IsSafe(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options;

    private static bool IsRetryable(int status) => status is 408 or 429 or >= 500;

    private static FailureClassification Classify(Exception exception) => exception switch
    {
        RetryableStatusException { Snapshot.StatusCode: 429 } limited => new FailureClassification(FailureKind.RateLimited, limited.Snapshot.RetryAfter),
        RetryableStatusException => new FailureClassification(FailureKind.Transient),
        ConnectorRefusedException => new FailureClassification(FailureKind.Refused, NotSent: true),
        HttpRequestException or TimeoutException or TaskCanceledException => new FailureClassification(FailureKind.Transient),
        _ => new FailureClassification(FailureKind.Permanent),
    };

    private static HttpRequestMessage Clone(HttpRequestMessage request, byte[]? body, KeyValuePair<string, IEnumerable<string>>[] contentHeaders)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version, VersionPolicy = request.VersionPolicy };

        foreach (var (name, values) in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(name, values);
        }

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var (name, values) in contentHeaders)
            {
                clone.Content.Headers.TryAddWithoutValidation(name, values);
            }
        }

        return clone;
    }

    private sealed class RetryableStatusException(ResponseSnapshot snapshot) : Exception($"HTTP {snapshot.StatusCode}")
    {
        public ResponseSnapshot Snapshot { get; } = snapshot;
    }

    /// <summary>One answer, read within the connection's response cap.</summary>
    private sealed class ResponseSnapshot
    {
        public int StatusCode { get; private init; }

        public string? ReasonPhrase { get; private init; }

        public Version Version { get; private init; } = HttpVersion.Version11;

        public byte[] Body { get; private init; } = [];

        public KeyValuePair<string, IEnumerable<string>>[] Headers { get; private init; } = [];

        public KeyValuePair<string, IEnumerable<string>>[] ContentHeaders { get; private init; } = [];

        public TimeSpan? RetryAfter { get; private init; }

        public static async Task<ResponseSnapshot> ReadAsync(HttpResponseMessage response, long maxBytes, CancellationToken cancellationToken)
        {
            if (response.Content.Headers.ContentLength is { } length && length > maxBytes)
            {
                throw new ConnectorRefusedException($"The response is larger than {maxBytes} bytes.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > maxBytes)
                {
                    throw new ConnectorRefusedException($"The response is larger than {maxBytes} bytes.");
                }
            }

            return new ResponseSnapshot
            {
                StatusCode = (int)response.StatusCode,
                ReasonPhrase = response.ReasonPhrase,
                Version = response.Version,
                Body = buffer.ToArray(),
                Headers = [.. response.Headers],
                ContentHeaders = [.. response.Content.Headers],
                RetryAfter = response.Headers.RetryAfter switch
                {
                    { Delta: { } delta } => delta,
                    { Date: { } date } => date - DateTimeOffset.UtcNow,
                    _ => null,
                },
            };
        }

        public HttpResponseMessage ToResponse(HttpRequestMessage request)
        {
            var response = new HttpResponseMessage((HttpStatusCode)StatusCode)
            {
                ReasonPhrase = ReasonPhrase,
                Version = Version,
                RequestMessage = request,
                Content = new ByteArrayContent(Body),
            };

            foreach (var (name, values) in Headers)
            {
                response.Headers.TryAddWithoutValidation(name, values);
            }

            foreach (var (name, values) in ContentHeaders)
            {
                if (!string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase))
                {
                    response.Content.Headers.TryAddWithoutValidation(name, values);
                }
            }

            return response;
        }
    }
}
