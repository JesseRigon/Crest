using System.Collections.Concurrent;
using System.Text;
using System.Threading.RateLimiting;
using Grove.Data.Protocols.Http;
using Microsoft.Extensions.Options;

namespace Grove.Data.Connectors.Services;

/// <summary>The shell's guarded HTTP client (a singleton of the tenant's container, disposed with it).</summary>
public sealed class ConnectorHttp(IOptions<ConnectorOptions> options) : IDisposable
{
    private readonly ConcurrentDictionary<string, GuardedHttpClient> _pinned = new(StringComparer.Ordinal);

    public GuardedHttpClient Client { get; } = new(options.Value.AllowPrivateNetworks);

    public ConnectorOptions Options => options.Value;

    /// <summary>The guarded client for a connection: the shared one, or one pinned to the connection's certificate fingerprints.</summary>
    public GuardedHttpClient For(ConnectionDefinition connection)
    {
        var pins = NetworkGuard.ParseFingerprints(connection.Setting(ConnectionSettingKeys.CertificateFingerprint) ?? connection.Setting(ConnectionSettingKeys.ElasticsearchFingerprint));
        if (pins.Count == 0)
        {
            return Client;
        }

        return _pinned.GetOrAdd(string.Join(',', pins.Order(StringComparer.Ordinal)), _ => new GuardedHttpClient(options.Value.AllowPrivateNetworks, pins));
    }

    public void Dispose()
    {
        Client.Dispose();
        foreach (var client in _pinned.Values)
        {
            client.Dispose();
        }
    }

    /// <summary>Reads a body up to <paramref name="max"/> bytes; more is a refusal.</summary>
    public static async Task<string?> ReadCappedAsync(HttpResponseMessage response, long max, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength is { } length && length > max)
        {
            throw new ConnectorRefusedException($"Response larger than {max} bytes.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length > max)
            {
                throw new ConnectorRefusedException($"Response larger than {max} bytes.");
            }
        }

        if (buffer.Length == 0)
        {
            return null;
        }

        var charset = response.Content.Headers.ContentType?.CharSet;
        Encoding encoding;
        try
        {
            encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            encoding = Encoding.UTF8;
        }

        return encoding.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}

/// <summary>
/// Per-shell rate limits (fixed windows per connection and credential owner, no queue) and
/// quotas (fixed periods per connection, counted on this node).
/// </summary>
public sealed class ConnectorLimits : IDisposable
{
    private readonly ConcurrentDictionary<string, FixedWindowRateLimiter> _limiters = new();
    private readonly ConcurrentDictionary<string, QuotaWindow> _quotas = new();

    private sealed class QuotaWindow
    {
        public DateTime StartUtc;
        public long Count;
    }

    /// <summary>Takes a permit; when none is left, how long until the window refills.</summary>
    public bool TryAcquire(ConnectionDefinition connection, CredentialOwner? owner, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (connection.Policy.RateLimit is not { Permits: > 0 } rate)
        {
            return true;
        }

        var limiter = _limiters.GetOrAdd($"{connection.Version}|{owner}|{rate.Permits}|{rate.Window.Ticks}", _ => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = rate.Permits,
            Window = rate.Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

        using var lease = limiter.AttemptAcquire();
        if (lease.IsAcquired)
        {
            return true;
        }

        retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? wait : rate.Window;
        return false;
    }

    /// <summary>Counts one call against the connection's quota; when spent, how long until the period ends.</summary>
    public bool TryCount(ConnectionDefinition connection, DateTime utcNow, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;
        if (connection.Policy.Quota is not { Limit: > 0 } quota)
        {
            return true;
        }

        var window = _quotas.GetOrAdd($"{connection.Key}|{quota.Limit}|{quota.Period.Ticks}", _ => new QuotaWindow { StartUtc = utcNow });
        lock (window)
        {
            if (utcNow - window.StartUtc >= quota.Period)
            {
                window.StartUtc = utcNow;
                window.Count = 0;
            }

            if (window.Count >= quota.Limit)
            {
                retryAfter = window.StartUtc + quota.Period - utcNow;
                return false;
            }

            window.Count++;
            return true;
        }
    }

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values)
        {
            limiter.Dispose();
        }
    }
}
