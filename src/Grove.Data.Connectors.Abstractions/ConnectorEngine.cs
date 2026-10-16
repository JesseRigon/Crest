using System.Text.Json.Nodes;
using Grove.Access;

namespace Grove.Data.Connectors;

/// <summary>
/// The one pipeline every outgoing call goes through: resolves the connection and the
/// caller's credential, enforces rate limit, quota and size caps, retries transient failures
/// per the connection's policy (honouring Retry-After), caches safe responses per credential,
/// and answers "retry after T" instead of sleeping through a long wait.
/// Never throws for a remote failure; the result says what happened.
/// </summary>
public interface IConnectorEngine
{
    Task<ConnectorResult> ExecuteAsync(ConnectorCall call, CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs a vendor SDK call inside the connection's credential and policy: the engine
    /// resolves the credential, applies rate limit and quota, hands the SDK a guarded HTTP
    /// client, and retries per policy using <see cref="SdkCallOptions.Classify"/>.
    /// </summary>
    Task<ConnectorResult<T>> RunSdkAsync<T>(
        string connectionKey,
        CallerContext caller,
        Func<ISdkCallContext, CancellationToken, Task<T>> call,
        SdkCallOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One call: an operation by id (with arguments), or a raw request under the base URL.
/// </summary>
public sealed record ConnectorCall
{
    public required string ConnectionKey { get; init; }

    /// <summary>Who the call runs as; decides the credential (<see cref="ICredentialResolver"/>).</summary>
    public required CallerContext Caller { get; init; }

    /// <summary>The operation to run; null for a raw <see cref="Request"/>.</summary>
    public string? OperationId { get; init; }

    /// <summary>Operation arguments by parameter name (path, query, header, body), or the SQL parameters.</summary>
    public JsonObject? Arguments { get; init; }

    /// <summary>A raw request (method, relative path, headers, body); used when <see cref="OperationId"/> is null, and overrides parts of an operation's request when both are set.</summary>
    public ConnectorRequest? Request { get; init; }

    /// <summary>The credential scope to call in; null takes the connection's default.</summary>
    public CredentialScope? Scope { get; init; }

    /// <summary>
    /// Makes an unsafe operation safe to retry: sent in the operation's idempotency header
    /// (when it supports one). A workflow passes its job key, so a resumed job reuses it.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Caps the connection's retry count for this call (0 = one attempt).</summary>
    public int? MaxRetries { get; init; }

    /// <summary>Lowers the connection's <see cref="ConnectionPolicy.MaxInlineWait"/> for this call; zero never waits inline.</summary>
    public TimeSpan? MaxInlineWait { get; init; }

    /// <summary>Skip the response cache (still stores the fresh response).</summary>
    public bool BypassCache { get; init; }

    /// <summary>The page position from a previous result's <see cref="ConnectorResult.NextPage"/>.</summary>
    public string? Page { get; init; }
}

/// <summary>A raw request under a connection's base URL. <see cref="Path"/> is relative and may carry a query; it may not leave the base URL.</summary>
public sealed record ConnectorRequest(
    string Method,
    string? Path,
    string? Body = null,
    string? ContentType = null,
    IReadOnlyDictionary<string, string>? Headers = null);

public enum ConnectorOutcome
{
    Succeeded,

    /// <summary>The service answered with a failure, or every retry failed.</summary>
    Failed,

    /// <summary>Not done yet: try again at <see cref="ConnectorResult.RetryAt"/> (a long Retry-After, an exhausted rate limit or quota window).</summary>
    RetryAfter,

    /// <summary>Refused before anything was sent: an address the guard refuses, a path leaving the base URL, a size cap, a caller who may not call.</summary>
    Refused,

    /// <summary>No usable credential: the caller must connect their account, or the tenant credential is missing or unreadable.</summary>
    NotAuthorized,
}

/// <summary>How a protocol classifies a failure; drives the retry decision.</summary>
public enum FailureKind
{
    None,

    /// <summary>Worth retrying (network error, timeout, 408, 5xx).</summary>
    Transient,

    /// <summary>The service said no for good (4xx other than 408/429, a SOAP client fault, a SQL syntax error).</summary>
    Permanent,

    /// <summary>The service or Grove's own limiter asked to slow down (429, a quota); see <see cref="FailureClassification.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary>401/403: the credential was refused (an OAuth grant gets one refresh then fails).</summary>
    Unauthorized,

    /// <summary>Refused before sending: never retried.</summary>
    Refused,
}

/// <summary>A protocol's verdict on one attempt.</summary>
public readonly record struct FailureClassification(FailureKind Kind, TimeSpan? RetryAfter = null, bool NotSent = false)
{
    public static readonly FailureClassification None = new(FailureKind.None);

    public bool IsFailure => Kind != FailureKind.None;

    /// <summary>Retrying could help (subject to the unsafe-operation rule).</summary>
    public bool IsRetryable => Kind is FailureKind.Transient or FailureKind.RateLimited;
}

/// <summary>One attempt the engine made, reported to the caller so run history shows it.</summary>
public sealed record ConnectorAttempt(int Number, DateTime StartedUtc, TimeSpan Duration, int StatusCode, FailureKind Failure, string? Error, TimeSpan? WaitBefore = null);

/// <summary>A call's outcome.</summary>
public record ConnectorResult
{
    public required ConnectorOutcome Outcome { get; init; }

    /// <summary>The protocol status (HTTP status; 0 when nothing was answered).</summary>
    public int StatusCode { get; init; }

    public string? Body { get; init; }

    /// <summary>The parsed body (JSON, a SOAP body as JSON, SQL rows as an array), when the protocol could parse it.</summary>
    public JsonNode? Value { get; init; }

    public IReadOnlyDictionary<string, string> Headers { get; init; } = EmptyHeaders;

    public string? Error { get; init; }

    public FailureKind Failure { get; init; }

    /// <summary>Set with <see cref="ConnectorOutcome.RetryAfter"/>: when to call again.</summary>
    public DateTimeOffset? RetryAt { get; init; }

    public IReadOnlyList<ConnectorAttempt> Attempts { get; init; } = [];

    public bool FromCache { get; init; }

    /// <summary>The next page position for a paged operation; null on the last page.</summary>
    public string? NextPage { get; init; }

    /// <summary>The credential owner the call ran as (never the secret).</summary>
    public CredentialOwner? RanAs { get; init; }

    public bool Succeeded => Outcome == ConnectorOutcome.Succeeded;

    private static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record ConnectorResult<T> : ConnectorResult
{
    public T? Result { get; init; }
}

/// <summary>What an SDK call gets: the connection, the caller's opened credential, and a guarded HTTP client.</summary>
public interface ISdkCallContext
{
    ConnectionDefinition Connection { get; }

    /// <summary>The resolved credential (null for auth scheme none); for the SDK's own client construction only.</summary>
    ConnectionCredential? Credential { get; }

    CredentialOwner? Owner { get; }

    /// <summary>1 for the first attempt.</summary>
    int Attempt { get; }

    /// <summary>
    /// An HTTP client on the guarded handler (address guard, no redirects, the connection's
    /// certificate pins), for SDKs that accept one. It is the engine's, shared by the tenant's
    /// calls on connections with the same pins, and outlives this call: never dispose it, and do
    /// not keep it to send requests later. The connection's policy (credential, retries, rate
    /// limit, quota, attempts) applies only to what is sent inside <see cref="IConnectorEngine.RunSdkAsync{T}"/>:
    /// every request a consumer sends must go through a <c>RunSdkAsync</c> call of its own.
    /// </summary>
    HttpClient HttpClient { get; }

    /// <summary>The guarded handler itself, for SDKs that take a handler. The same lifetime rules as <see cref="HttpClient"/>: never disposed by the SDK, never used outside the call.</summary>
    HttpMessageHandler Handler { get; }

    /// <summary>A current OAuth access token for the credential (refreshed when due); null for non-OAuth schemes.</summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    IServiceProvider Services { get; }
}

public sealed record SdkCallOptions
{
    /// <summary>The call is safe to repeat (a read); an unsafe one is retried only on <see cref="FailureClassification.NotSent"/>.</summary>
    public bool IsSafeToRetry { get; init; }

    public CredentialScope? Scope { get; init; }

    public int? MaxRetries { get; init; }

    public TimeSpan? MaxInlineWait { get; init; }

    /// <summary>Classifies an SDK exception; the default treats <see cref="HttpRequestException"/> and timeouts as transient and everything else as permanent.</summary>
    public Func<Exception, FailureClassification>? Classify { get; init; }
}
