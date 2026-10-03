using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crest.Workflows.Connectors;

/// <summary>Refused before a byte was sent: never retried.</summary>
public sealed class ConnectorRefusedException(string message) : Exception(message);

/// <summary>
/// Which addresses a connector may reach. Checked on the address actually connected to
/// (<see cref="ConnectorHttpClient"/>'s connect callback), after DNS, so a public name that
/// resolves to a private address - or re-resolves to one later - is refused as well.
/// </summary>
public static class ConnectorNetworkPolicy
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None))
        {
            return false;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)   // carrier-grade NAT
                || (b[0] == 169 && b[1] == 254)                 // link-local, cloud metadata
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))  // benchmarking
                || b[0] >= 224);                                // multicast, reserved, broadcast
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var b = address.GetAddressBytes();
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (b[0] & 0xFE) == 0xFC);                      // unique local fc00::/7
        }

        return false;
    }
}

/// <summary>
/// The shell's one HTTP client for connectors: pooled, no automatic redirects (a redirect
/// could point a credentialed call at another host), and every connection vetted by
/// <see cref="ConnectorNetworkPolicy"/> unless the shell allows private networks.
/// A singleton of the tenant's container, disposed with the shell.
/// </summary>
public sealed class ConnectorHttpClient : IDisposable
{
    public ConnectorHttpClient(IOptions<WorkflowConnectorOptions> options)
    {
        var allowPrivate = options.Value.AllowPrivateNetworks;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
                if (addresses.Length == 0)
                {
                    throw new ConnectorRefusedException($"'{context.DnsEndPoint.Host}' does not resolve.");
                }

                if (!allowPrivate && addresses.Any(a => !ConnectorNetworkPolicy.IsPublic(a)))
                {
                    throw new ConnectorRefusedException($"'{context.DnsEndPoint.Host}' resolves to a non-public address; connectors may only call public services.");
                }

                var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                try
                {
                    await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            },
        };

        Client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public HttpClient Client { get; }

    public void Dispose() => Client.Dispose();
}

/// <summary>Per-connection request rate limits for the shell (fixed one-minute windows, no queue).</summary>
public sealed class ConnectorRateLimiters : IDisposable
{
    private readonly ConcurrentDictionary<string, RateLimiter> _limiters = new();

    public bool TryAcquire(WorkflowConnection connection)
    {
        if (connection.RateLimitPerMinute is not { } limit)
        {
            return true;
        }

        var limiter = _limiters.GetOrAdd($"{connection.Version}:{limit}", _ => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
        {
            PermitLimit = limit,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));

        using var lease = limiter.AttemptAcquire();
        return lease.IsAcquired;
    }

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values)
        {
            limiter.Dispose();
        }
    }
}

/// <summary>OAuth2 client-credentials tokens for the shell, per connection version, until shortly before expiry.</summary>
public sealed class ConnectorTokenCache
{
    private readonly ConcurrentDictionary<string, (string Token, DateTime ExpiresUtc)> _tokens = new();

    public string? Find(WorkflowConnection connection) =>
        _tokens.TryGetValue(connection.Version, out var entry) && entry.ExpiresUtc > DateTime.UtcNow ? entry.Token : null;

    public void Store(WorkflowConnection connection, string token, int expiresInSeconds) =>
        _tokens[connection.Version] = (token, DateTime.UtcNow.AddSeconds(Math.Max(0, expiresInSeconds - 30)));
}

public sealed record ConnectorRequest(string Method, string? Path, string? Body, string? ContentType, IReadOnlyDictionary<string, string>? Headers);

/// <summary>One attempt's outcome. <see cref="Transient"/> marks a failure worth retrying (network, timeout, 408, 429, 5xx); the connection's resilience strategy decides whether to.</summary>
public sealed record ConnectorResponse(bool Succeeded, int StatusCode, string? Body, string? Error, bool Transient = false)
{
    public static ConnectorResponse Failed(string error, int statusCode = 0, string? body = null, bool transient = false) => new(false, statusCode, body, error, transient);
}

/// <summary>
/// Calls a tenant's connection once: resolves the path against the base URL (and refuses one
/// that leaves it), injects the auth, applies the rate limit, and caps the response it reads.
/// Never throws for a remote failure; the caller gets a failed <see cref="ConnectorResponse"/>,
/// marked transient when a retry could help. Retrying is the engine's resilience feature's
/// job (<see cref="ConnectionResilienceStrategy"/> reads the connection's policy), so the
/// attempts are journaled like any other activity's.
/// </summary>
public sealed class ConnectorInvoker(
    ConnectorHttpClient http,
    ConnectorRateLimiters rateLimiters,
    ConnectorTokenCache tokens,
    WorkflowConnectionService connections,
    ConnectorOAuthClient oauth,
    IOptions<WorkflowConnectorOptions> options,
    ILogger<ConnectorInvoker> logger)
{
    public async Task<ConnectorResponse> SendAsync(WorkflowConnection connection, ConnectorRequest request, CancellationToken cancellationToken)
    {
        if (!TryResolve(connection, request.Path, out var uri, out var error))
        {
            return ConnectorResponse.Failed(error);
        }

        if (!rateLimiters.TryAcquire(connection))
        {
            return ConnectorResponse.Failed($"Rate limit of {connection.RateLimitPerMinute} requests per minute reached for connection '{connection.Key}'.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(connection.TimeoutSeconds));
        ConnectorResponse failed;
        try
        {
            using var message = new HttpRequestMessage(new HttpMethod(request.Method.ToUpperInvariant()), uri);
            if (request.Body is not null && message.Method != HttpMethod.Get && message.Method != HttpMethod.Head)
            {
                message.Content = new StringContent(request.Body, Encoding.UTF8);
                message.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(request.ContentType) ? "application/json" : request.ContentType);
            }

            foreach (var (name, value) in request.Headers ?? new Dictionary<string, string>())
            {
                if (!string.Equals(name, "Host", StringComparison.OrdinalIgnoreCase))
                {
                    message.Headers.TryAddWithoutValidation(name, value);
                }
            }

            var authError = await AuthorizeAsync(connection, message, timeout.Token);
            if (authError is not null)
            {
                return ConnectorResponse.Failed(authError);
            }

            using var response = await http.Client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var body = await ReadCappedAsync(response, timeout.Token);
            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
            {
                return new ConnectorResponse(true, status, body, null);
            }

            failed = ConnectorResponse.Failed($"HTTP {status} from {uri.Host}.", status, body, IsTransient(status));
        }
        catch (Exception ex) when (FindRefusal(ex) is { } refusal)
        {
            return ConnectorResponse.Failed(refusal.Message);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            failed = ConnectorResponse.Failed($"Timed out after {connection.TimeoutSeconds}s calling {uri.Host}.", transient: true);
        }
        catch (HttpRequestException ex)
        {
            failed = ConnectorResponse.Failed($"{uri.Host}: {ex.Message}", transient: true);
        }

        logger.LogInformation("Connector {Connection} call failed{Transient}: {Error}", connection.Key, failed.Transient ? " (transient)" : string.Empty, failed.Error);
        return failed;
    }

    /// <summary>The request URI: <paramref name="path"/> (relative, may carry a query) under the base URL, never outside it.</summary>
    public static bool TryResolve(WorkflowConnection connection, string? path, out Uri uri, out string error)
    {
        uri = null!;
        error = string.Empty;
        if (!Uri.TryCreate(connection.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            error = $"Connection '{connection.Key}' has no base URL.";
            return false;
        }

        path = path?.Trim() ?? string.Empty;
        if (path.Contains("://", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal) || path.StartsWith('\\'))
        {
            error = "The path must be relative to the connection's base URL.";
            return false;
        }

        var root = baseUri.AbsoluteUri.EndsWith('/') ? baseUri : new Uri(baseUri.AbsoluteUri + "/");
        uri = new Uri(root, path.TrimStart('/'));
        if (uri.Scheme != root.Scheme || !string.Equals(uri.Authority, root.Authority, StringComparison.OrdinalIgnoreCase)
            || !uri.AbsolutePath.StartsWith(root.AbsolutePath, StringComparison.Ordinal))
        {
            error = "The path leaves the connection's base URL.";
            return false;
        }

        return true;
    }

    private async Task<string?> AuthorizeAsync(WorkflowConnection connection, HttpRequestMessage message, CancellationToken cancellationToken)
    {
        var secret = connections.RevealSecret(connection);
        switch (connection.AuthKind)
        {
            case WorkflowConnectorAuthKinds.ApiKey:
                var header = connection.Setting(WorkflowConnectionSettingKeys.HeaderName) ?? WorkflowConnectionSettingKeys.DefaultApiKeyHeader;
                message.Headers.Remove(header);
                message.Headers.TryAddWithoutValidation(header, secret);
                break;
            case WorkflowConnectorAuthKinds.Basic:
                var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connection.Setting(WorkflowConnectionSettingKeys.Username)}:{secret}"));
                message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
                break;
            case WorkflowConnectorAuthKinds.Bearer:
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                break;
            case WorkflowConnectorAuthKinds.OAuth2ClientCredentials:
                var token = tokens.Find(connection);
                if (token is null)
                {
                    (token, var error) = await RequestTokenAsync(connection, secret, cancellationToken);
                    if (token is null)
                    {
                        return error;
                    }
                }

                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                break;
            case WorkflowConnectorAuthKinds.OAuth2AuthorizationCode:
                var stored = connections.ReadTokens(connection);
                if (stored is null)
                {
                    return $"Connection '{connection.Key}' is not authorized yet: a user must authorize it (connections/{connection.Key}/oauth/authorize).";
                }

                if (stored.ExpiresUtc <= DateTime.UtcNow)
                {
                    if (stored.RefreshToken is null)
                    {
                        return $"Connection '{connection.Key}': the authorization expired and the provider gave no refresh token; authorize it again.";
                    }

                    var (refreshed, refreshError) = await oauth.RefreshAsync(connection, stored.RefreshToken, cancellationToken);
                    if (refreshed is null)
                    {
                        return $"Connection '{connection.Key}': refreshing the authorization failed - {refreshError}";
                    }

                    await connections.StoreTokensAsync(connection.Key, refreshed);
                    stored = refreshed;
                }

                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stored.AccessToken);
                break;
            case WorkflowConnectorAuthKinds.Hmac:
                return $"Connection '{connection.Key}' is for inbound webhooks (hmac); it cannot make calls.";
        }

        return null;
    }

    private async Task<(string? Token, string? Error)> RequestTokenAsync(WorkflowConnection connection, string? secret, CancellationToken cancellationToken)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = connection.Setting(WorkflowConnectionSettingKeys.ClientId) ?? string.Empty,
            ["client_secret"] = secret ?? string.Empty,
        };
        if (connection.Setting(WorkflowConnectionSettingKeys.Scope) is { } scope)
        {
            form["scope"] = scope;
        }

        using var response = await http.Client.PostAsync(connection.Setting(WorkflowConnectionSettingKeys.TokenUrl), new FormUrlEncodedContent(form), cancellationToken);
        var body = await ReadCappedAsync(response, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return (null, $"Token endpoint answered HTTP {(int)response.StatusCode}.");
        }

        try
        {
            using var json = JsonDocument.Parse(body ?? "{}");
            var token = json.RootElement.TryGetProperty("access_token", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(token))
            {
                return (null, "Token endpoint returned no access_token.");
            }

            var expiresIn = json.RootElement.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var seconds) ? seconds : 3600;
            tokens.Store(connection, token, expiresIn);
            return (token, null);
        }
        catch (JsonException)
        {
            return (null, "Token endpoint returned no JSON.");
        }
    }

    private async Task<string?> ReadCappedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var max = options.Value.MaxResponseBytes;
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[max + 1];
        var read = 0;
        int n;
        while (read <= max && (n = await stream.ReadAsync(buffer.AsMemory(read, buffer.Length - read), cancellationToken)) > 0)
        {
            read += n;
        }

        if (read > max)
        {
            throw new ConnectorRefusedException($"Response larger than {max} bytes.");
        }

        return read == 0 ? null : Encoding.UTF8.GetString(buffer, 0, read);
    }

    private static bool IsTransient(int status) => status is 408 or 429 || status >= 500;

    private static ConnectorRefusedException? FindRefusal(Exception? ex)
    {
        for (; ex is not null; ex = ex.InnerException)
        {
            if (ex is ConnectorRefusedException refused) return refused;
        }

        return null;
    }
}
