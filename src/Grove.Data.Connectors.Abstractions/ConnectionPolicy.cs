namespace Grove.Data.Connectors;

/// <summary>
/// How the engine runs a connection's calls: retries and backoff, timeout, rate limit, quota,
/// cache lifetime and size caps. Set by the connection's owner, not by each caller.
/// </summary>
public sealed record ConnectionPolicy
{
    /// <summary>Retries after the first attempt for a transient or rate-limited failure (0-10).</summary>
    public int RetryCount { get; init; } = 2;

    public BackoffPolicy Backoff { get; init; } = new();

    /// <summary>Per attempt.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);

    public RateLimitPolicy? RateLimit { get; init; }

    public QuotaPolicy? Quota { get; init; }

    /// <summary>How long a safe operation's response is cached, per credential; null or zero means no cache.</summary>
    public TimeSpan? CacheLifetime { get; init; }

    public long MaxResponseBytes { get; init; } = 1024 * 1024;

    public long MaxRequestBytes { get; init; } = 1024 * 1024;

    /// <summary>
    /// The longest wait (backoff or Retry-After) the engine sleeps through inside one call.
    /// A longer one ends the call with <see cref="ConnectorOutcome.RetryAfter"/> and the caller
    /// reschedules. A call may lower it (<see cref="ConnectorCall.MaxInlineWait"/>).
    /// </summary>
    public TimeSpan MaxInlineWait { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// On a connection that allows the user scope: when the caller has no credential of their
    /// own, use the tenant's. Off by default: a per-user call never silently becomes the tenant's.
    /// </summary>
    public bool AllowTenantFallback { get; init; }
}

/// <summary>Exponential backoff with jitter between attempts.</summary>
public sealed record BackoffPolicy
{
    public TimeSpan Initial { get; init; } = TimeSpan.FromMilliseconds(200);

    public TimeSpan Max { get; init; } = TimeSpan.FromSeconds(30);

    public double Factor { get; init; } = 2;

    public bool Jitter { get; init; } = true;
}

/// <summary>At most <see cref="Permits"/> requests per <see cref="Window"/>, per connection and credential.</summary>
public sealed record RateLimitPolicy(int Permits, TimeSpan Window);

/// <summary>At most <see cref="Limit"/> requests per <see cref="Period"/> per connection (tenant-wide), counted durably.</summary>
public sealed record QuotaPolicy(long Limit, TimeSpan Period);
