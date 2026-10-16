using Grove.Access;

namespace Grove.Data.Connectors;

/// <summary>The tokens an OAuth grant holds. Sealed in the credential store; never returned.</summary>
public sealed record OAuthTokenSet(
    string AccessToken,
    string? RefreshToken,
    DateTime ExpiresUtc,
    string TokenType = "Bearer",
    IReadOnlyList<string>? Scopes = null,
    string? IdToken = null)
{
    /// <summary>Due for a refresh: expired or expiring within <paramref name="skew"/>.</summary>
    public bool IsDue(DateTime utcNow, TimeSpan skew) => ExpiresUtc - skew <= utcNow;

    public override string ToString() => "[tokens]";
}

/// <summary>A token endpoint's answer: tokens, or an OAuth error (<c>invalid_grant</c>, <c>authorization_pending</c>, ...).</summary>
public sealed record OAuthTokenResult(OAuthTokenSet? Tokens, string? ErrorCode = null, string? Error = null)
{
    public bool Succeeded => Tokens is not null;
}

/// <summary>A device authorization's start: what the user enters where, and how to poll.</summary>
public sealed record DeviceAuthorization(string DeviceCode, string UserCode, string VerificationUri, string? VerificationUriComplete, DateTime ExpiresUtc, TimeSpan Interval);

/// <summary>
/// The OAuth 2.0 client flows (<c>Grove.Data.Protocols.OAuth</c>): stateless, run on the
/// guarded client the engine passes in. The engine owns storing, refreshing and revoking.
/// </summary>
public interface IOAuthClient
{
    /// <summary>The authorization URL (authorization code), with the state and, when the connection uses PKCE, the S256 challenge of <paramref name="codeVerifier"/>.</summary>
    Uri BuildAuthorizeUrl(ConnectionDefinition connection, AppCredentials app, string redirectUri, string state, string? codeVerifier);

    Task<OAuthTokenResult> ExchangeCodeAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, string code, string redirectUri, string? codeVerifier, CancellationToken cancellationToken = default);

    Task<OAuthTokenResult> RefreshAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, string refreshToken, CancellationToken cancellationToken = default);

    Task<OAuthTokenResult> RequestClientCredentialsAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, CancellationToken cancellationToken = default);

    Task<DeviceAuthorization?> StartDeviceAuthorizationAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, CancellationToken cancellationToken = default);

    /// <summary>Polls once; <see cref="OAuthTokenResult.ErrorCode"/> <c>authorization_pending</c> or <c>slow_down</c> means poll again.</summary>
    Task<OAuthTokenResult> PollDeviceTokenAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, string deviceCode, CancellationToken cancellationToken = default);

    /// <summary>Revokes a token at the provider (RFC 7009); false when the connection has no revocation endpoint or the provider refused.</summary>
    Task<bool> RevokeAsync(HttpClient http, ConnectionDefinition connection, AppCredentials app, string token, bool isRefreshToken, CancellationToken cancellationToken = default);

    /// <summary>A new PKCE code verifier.</summary>
    string CreateCodeVerifier();
}

/// <summary>The state an authorize round-trip carries, sealed so the callback trusts only what Grove issued.</summary>
public sealed record OAuthState(
    string Tenant,
    string ConnectionKey,
    CredentialOwner Owner,
    string UserId,
    DateTime IssuedUtc,
    string? CodeVerifier,
    string? ReturnUrl);

/// <summary>Seals and opens <see cref="OAuthState"/> with the tenant's data protection, time-limited.</summary>
public interface IOAuthStateProtector
{
    string Protect(OAuthState state);

    /// <summary>Null when tampered with, from another tenant, or older than the lifetime.</summary>
    OAuthState? Unprotect(string protectedState);
}

/// <summary>The outcome of an authorization step.</summary>
public sealed record ConnectionAuthorizationResult(bool Succeeded, string? Error = null, CredentialInfo? Credential = null, string? ReturnUrl = null);

/// <summary>
/// The engine's OAuth lifecycle for a connection: authorize and callback (authorization code),
/// device code, and revoke. Refreshing happens inside calls. Refuses member-shell callers.
/// </summary>
public interface IConnectionAuthorizationService
{
    /// <summary>The provider URL to send the caller to, for their own credential in <paramref name="scope"/> (tenant needs <c>ManageConnections</c>).</summary>
    Task<(Uri? Url, string? Error)> BeginAsync(string connectionKey, CallerContext caller, CredentialScope scope, string redirectUri, string? returnUrl, CancellationToken cancellationToken = default);

    /// <summary>The callback: opens the state, checks the caller is the user who began it, exchanges the code and stores the grant.</summary>
    Task<ConnectionAuthorizationResult> CompleteAsync(CallerContext caller, string protectedState, string? code, string? error, string redirectUri, CancellationToken cancellationToken = default);

    Task<(DeviceAuthorization? Authorization, string? Error)> BeginDeviceAsync(string connectionKey, CallerContext caller, CredentialScope scope, CancellationToken cancellationToken = default);

    Task<ConnectionAuthorizationResult> PollDeviceAsync(string connectionKey, CallerContext caller, CredentialScope scope, string deviceCode, CancellationToken cancellationToken = default);

    /// <summary>Revokes at the provider (best effort) and deletes the stored grant.</summary>
    Task<bool> RevokeAsync(string connectionKey, CallerContext caller, CredentialOwner owner, CancellationToken cancellationToken = default);
}
