using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using Grove.Data.Connectors;

namespace Grove.Data.Protocols.Http;

/// <summary>How HTTP signals failure: 408 and 5xx are transient, 429 is a rate limit (with Retry-After), 401/403 an unauthorized credential, other 4xx permanent.</summary>
public static class HttpFailures
{
    public static FailureClassification Classify(int status, HttpResponseHeaders? headers = null, DateTimeOffset? now = null)
    {
        if (status is >= 200 and < 400)
        {
            return FailureClassification.None;
        }

        var retryAfter = headers is null ? null : RetryAfter(headers.RetryAfter, now ?? DateTimeOffset.UtcNow);
        return status switch
        {
            429 => new FailureClassification(FailureKind.RateLimited, retryAfter),
            503 when retryAfter is not null => new FailureClassification(FailureKind.RateLimited, retryAfter),
            408 or >= 500 => new FailureClassification(FailureKind.Transient, retryAfter),
            401 or 403 => new FailureClassification(FailureKind.Unauthorized),
            _ => new FailureClassification(FailureKind.Permanent),
        };
    }

    /// <summary>The wait a <c>Retry-After</c> header asks for (delta seconds or an HTTP date), never negative.</summary>
    public static TimeSpan? RetryAfter(RetryConditionHeaderValue? value, DateTimeOffset now)
    {
        if (value is null)
        {
            return null;
        }

        if (value.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (value.Date is { } date)
        {
            var wait = date - now;
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    /// <summary>Parses a raw <c>Retry-After</c> value.</summary>
    public static TimeSpan? RetryAfter(string? value, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            return TimeSpan.FromSeconds(Math.Max(0, seconds));
        }

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date)
            ? (date - now < TimeSpan.Zero ? TimeSpan.Zero : date - now)
            : null;
    }

    /// <summary>
    /// An exception an attempt threw. A refusal, a name that does not resolve and a refused
    /// connection prove the request was never sent; a timeout or a reset mid-way do not.
    /// </summary>
    public static FailureClassification Classify(Exception exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is ConnectorRefusedException)
            {
                return new FailureClassification(FailureKind.Refused, NotSent: true);
            }
        }

        if (exception is HttpRequestException http)
        {
            var notSent = http.HttpRequestError is HttpRequestError.NameResolutionError or HttpRequestError.ConnectionError or HttpRequestError.SecureConnectionError
                || http.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused or SocketError.HostNotFound or SocketError.HostUnreachable or SocketError.NetworkUnreachable };
            return new FailureClassification(FailureKind.Transient, NotSent: notSent);
        }

        if (exception is OperationCanceledException or TimeoutException or IOException)
        {
            return new FailureClassification(FailureKind.Transient);
        }

        return new FailureClassification(FailureKind.Permanent);
    }

    public static bool IsSuccess(HttpStatusCode status) => (int)status is >= 200 and < 300;
}
