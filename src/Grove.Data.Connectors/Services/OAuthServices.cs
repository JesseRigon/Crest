using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Grove.Access;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Grove.Data.Connectors.Services;

/// <summary>No usable token: the caller must (re)connect. Raised inside an attempt, answered as <see cref="ConnectorOutcome.NotAuthorized"/>.</summary>
public sealed class ConnectorNotAuthorizedException(string message) : Exception(message);

/// <summary>
/// OAuth tokens for calls: client-credentials tokens cached per connection version until
/// shortly before expiry; authorization-code and device-code grants refreshed when due (or
/// when the service refused the token) and stored back sealed.
/// </summary>
public sealed class ConnectorTokenService(
    IOAuthClient oauth,
    ConnectorHttp http,
    ICredentialStore store,
    AppCredentialsProvider apps,
    ConnectorTokenCache cache)
{
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(60);

    public async Task<string> GetAccessTokenAsync(ConnectionDefinition connection, CredentialOwner? owner, ConnectionCredential? credential, bool forceRefresh, CancellationToken cancellationToken)
    {
        switch (connection.Auth.Scheme)
        {
            case AuthSchemes.OAuth2ClientCredentials:
                var cacheKey = $"{connection.Version}|{owner}";
                if (!forceRefresh && cache.Find(cacheKey) is { } cached)
                {
                    return cached;
                }

                var app = await apps.ForConnectionAsync(connection, cancellationToken)
                    ?? throw new ConnectorNotAuthorizedException($"Connection '{connection.Key}' has no client id and secret.");
                var issued = await oauth.RequestClientCredentialsAsync(http.Client.Client, connection, app, cancellationToken);
                if (issued.Tokens is not { } tokens)
                {
                    throw new ConnectorNotAuthorizedException($"Connection '{connection.Key}': the token endpoint refused the client credentials ({issued.Error}).");
                }

                cache.Store(cacheKey, tokens.AccessToken, tokens.ExpiresUtc - Skew);
                return tokens.AccessToken;

            case AuthSchemes.OAuth2AuthorizationCode or AuthSchemes.OAuth2DeviceCode:
                if (owner is not { } grantOwner || credential?.Tokens is not { } grant)
                {
                    throw new ConnectorNotAuthorizedException($"Connection '{connection.Key}' is not authorized yet: connect the account first.");
                }

                if (!forceRefresh && !grant.IsDue(DateTime.UtcNow, Skew))
                {
                    return grant.AccessToken;
                }

                if (grant.RefreshToken is null)
                {
                    throw new ConnectorNotAuthorizedException($"Connection '{connection.Key}': the authorization expired and the provider gave no refresh token; connect the account again.");
                }

                var client = await apps.ForConnectionAsync(connection, cancellationToken)
                    ?? throw new ConnectorNotAuthorizedException($"Connection '{connection.Key}' has no client id.");
                var refreshed = await oauth.RefreshAsync(http.Client.Client, connection, client, grant.RefreshToken, cancellationToken);
                if (refreshed.Tokens is not { } fresh)
                {
                    throw new ConnectorNotAuthorizedException($"Connection '{connection.Key}': refreshing the authorization failed ({refreshed.Error}); connect the account again.");
                }

                await store.StoreTokensAsync(connection.Key, grantOwner, fresh, cancellationToken);
                return fresh.AccessToken;

            default:
                throw new InvalidOperationException($"'{connection.Auth.Scheme}' is not an OAuth scheme.");
        }
    }
}

/// <summary>Client-credentials tokens of the shell (a singleton of the tenant's container).</summary>
public sealed class ConnectorTokenCache
{
    private readonly ConcurrentDictionary<string, (string Token, DateTime ValidUntilUtc)> _tokens = new();

    public string? Find(string key) => _tokens.TryGetValue(key, out var entry) && entry.ValidUntilUtc > DateTime.UtcNow ? entry.Token : null;

    public void Store(string key, string token, DateTime validUntilUtc) => _tokens[key] = (token, validUntilUtc);
}

/// <summary>Seals the OAuth state with the tenant's data protection (time-limited, bound to the tenant).</summary>
public sealed class OAuthStateProtector(IDataProtectionProvider dataProtection, IOptions<ConnectorOptions> options, Grove.Environment.Shell.ShellSettings shell) : IOAuthStateProtector
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private ITimeLimitedDataProtector Protector => dataProtection.CreateProtector(ConnectorsConstants.OAuthStateProtectionPurpose).ToTimeLimitedDataProtector();

    public string Protect(OAuthState state) => Protector.Protect(JsonSerializer.Serialize(state, Json), options.Value.OAuthStateLifetime);

    public OAuthState? Unprotect(string protectedState)
    {
        try
        {
            var state = JsonSerializer.Deserialize<OAuthState>(Protector.Unprotect(protectedState), Json);
            return state is not null && string.Equals(state.Tenant, shell.Name, StringComparison.Ordinal) ? state : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or FormatException)
        {
            return null;
        }
    }
}

/// <summary>
/// The OAuth lifecycle of connections: authorize and callback (authorization code, PKCE),
/// device code, revocation. Staff callers only; a tenant grant takes <c>ManageConnections</c>,
/// a user grant is the caller's own.
/// </summary>
public sealed class ConnectionAuthorizationService(
    IConnectionRegistry registry,
    IOAuthClient oauth,
    IOAuthStateProtector states,
    ICredentialStore store,
    AppCredentialsProvider apps,
    ConnectorHttp http,
    IEnumerable<ICredentialScopeHandler> handlers) : IConnectionAuthorizationService
{
    public async Task<(Uri? Url, string? Error)> BeginAsync(string connectionKey, CallerContext caller, CredentialScope scope, string redirectUri, string? returnUrl, CancellationToken cancellationToken = default)
    {
        var (connection, owner, error) = await PrepareAsync(connectionKey, caller, scope, AuthSchemes.OAuth2AuthorizationCode, cancellationToken);
        if (error is not null)
        {
            return (null, error);
        }

        var app = await apps.ForConnectionAsync(connection!, cancellationToken);
        if (app is null)
        {
            return (null, $"Connection '{connectionKey}' has no client id: enter its app credentials first.");
        }

        var verifier = connection!.Auth.UsePkce ? oauth.CreateCodeVerifier() : null;
        var state = states.Protect(new OAuthState(caller.Tenant, connection.Key, owner!.Value, caller.UserId!, DateTime.UtcNow, verifier, returnUrl));
        return (oauth.BuildAuthorizeUrl(connection, app, redirectUri, state, verifier), null);
    }

    public async Task<ConnectionAuthorizationResult> CompleteAsync(CallerContext caller, string protectedState, string? code, string? error, string redirectUri, CancellationToken cancellationToken = default)
    {
        if (!ConnectorCallers.IsStaffUser(caller))
        {
            return new(false, "Connections are authorized from the admin side only.");
        }

        var state = states.Unprotect(protectedState);
        if (state is null)
        {
            return new(false, "The authorization state is invalid or expired; start again.");
        }

        if (!string.Equals(state.UserId, caller.UserId, StringComparison.Ordinal) || !string.Equals(state.Tenant, caller.Tenant, StringComparison.Ordinal))
        {
            return new(false, "The authorization was started by someone else.");
        }

        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code))
        {
            return new(false, $"The provider refused the authorization: {error}", ReturnUrl: state.ReturnUrl);
        }

        var connection = await registry.FindAsync(state.ConnectionKey, cancellationToken);
        if (connection is null)
        {
            return new(false, $"No connection '{state.ConnectionKey}'.");
        }

        var handler = handlers.FirstOrDefault(h => h.Scope == state.Owner.Scope);
        if (handler is null || !handler.CanManage(caller, state.Owner))
        {
            return new(false, "You may not authorize this credential.");
        }

        var app = await apps.ForConnectionAsync(connection, cancellationToken);
        if (app is null)
        {
            return new(false, $"Connection '{connection.Key}' has no client id.");
        }

        var exchanged = await oauth.ExchangeCodeAsync(http.Client.Client, connection, app, code, redirectUri, state.CodeVerifier, cancellationToken);
        if (exchanged.Tokens is not { } tokens)
        {
            return new(false, $"The token exchange failed: {exchanged.Error}", ReturnUrl: state.ReturnUrl);
        }

        await store.StoreTokensAsync(connection.Key, state.Owner, tokens, cancellationToken);
        return new(true, Credential: await store.FindAsync(connection.Key, state.Owner, cancellationToken), ReturnUrl: state.ReturnUrl);
    }

    public async Task<(DeviceAuthorization? Authorization, string? Error)> BeginDeviceAsync(string connectionKey, CallerContext caller, CredentialScope scope, CancellationToken cancellationToken = default)
    {
        var (connection, _, error) = await PrepareAsync(connectionKey, caller, scope, AuthSchemes.OAuth2DeviceCode, cancellationToken);
        if (error is not null)
        {
            return (null, error);
        }

        var app = await apps.ForConnectionAsync(connection!, cancellationToken);
        if (app is null)
        {
            return (null, $"Connection '{connectionKey}' has no client id.");
        }

        var started = await oauth.StartDeviceAuthorizationAsync(http.Client.Client, connection!, app, cancellationToken);
        return started is null ? (null, "The provider refused the device authorization.") : (started, null);
    }

    public async Task<ConnectionAuthorizationResult> PollDeviceAsync(string connectionKey, CallerContext caller, CredentialScope scope, string deviceCode, CancellationToken cancellationToken = default)
    {
        var (connection, owner, error) = await PrepareAsync(connectionKey, caller, scope, AuthSchemes.OAuth2DeviceCode, cancellationToken);
        if (error is not null)
        {
            return new(false, error);
        }

        var app = await apps.ForConnectionAsync(connection!, cancellationToken);
        if (app is null)
        {
            return new(false, $"Connection '{connectionKey}' has no client id.");
        }

        var polled = await oauth.PollDeviceTokenAsync(http.Client.Client, connection!, app, deviceCode, cancellationToken);
        if (polled.Tokens is not { } tokens)
        {
            return new(false, polled.ErrorCode is "authorization_pending" or "slow_down" ? "pending" : polled.Error);
        }

        await store.StoreTokensAsync(connection!.Key, owner!.Value, tokens, cancellationToken);
        return new(true, Credential: await store.FindAsync(connection.Key, owner.Value, cancellationToken));
    }

    public async Task<bool> RevokeAsync(string connectionKey, CallerContext caller, CredentialOwner owner, CancellationToken cancellationToken = default)
    {
        var handler = handlers.FirstOrDefault(h => h.Scope == owner.Scope);
        if (handler is null || !handler.CanManage(caller, owner))
        {
            return false;
        }

        var connection = await registry.FindAsync(connectionKey, cancellationToken);
        if (connection is not null && AuthSchemes.IsOAuth2(connection.Auth.Scheme))
        {
            try
            {
                var credential = await store.RevealAsync(connectionKey, owner, cancellationToken);
                if (credential?.Tokens is { } tokens && await apps.ForConnectionAsync(connection, cancellationToken) is { } app)
                {
                    // Best effort: the grant is deleted here whatever the provider answers.
                    await oauth.RevokeAsync(http.Client.Client, connection, app, tokens.RefreshToken ?? tokens.AccessToken, tokens.RefreshToken is not null, cancellationToken);
                }
            }
            catch (Exception ex) when (ex is CredentialUnreadableException or HttpRequestException or ConnectorRefusedException or TaskCanceledException)
            {
                // An unreadable or unreachable grant is still deleted.
            }
        }

        return await store.DeleteAsync(connectionKey, owner, cancellationToken);
    }

    private async Task<(ConnectionDefinition? Connection, CredentialOwner? Owner, string? Error)> PrepareAsync(string connectionKey, CallerContext caller, CredentialScope scope, string scheme, CancellationToken cancellationToken)
    {
        if (!ConnectorCallers.IsStaffUser(caller))
        {
            return (null, null, "Connections are authorized from the admin side only.");
        }

        var connection = await registry.FindAsync(connectionKey, cancellationToken);
        if (connection is null)
        {
            return (null, null, $"No connection '{connectionKey}'.");
        }

        if (connection.Auth.Scheme != scheme)
        {
            return (null, null, $"Connection '{connectionKey}' uses '{connection.Auth.Scheme}', not '{scheme}'.");
        }

        if (!connection.Allows(scope))
        {
            return (null, null, $"Connection '{connectionKey}' does not allow {scope.Value} credentials.");
        }

        var handler = handlers.FirstOrDefault(h => h.Scope == scope);
        var owner = handler?.OwnerFor(caller);
        if (handler is null || owner is null || !handler.CanManage(caller, owner.Value))
        {
            return (null, null, $"You may not authorize the {scope.Value} credential of '{connectionKey}'.");
        }

        return (connection, owner, null);
    }
}
