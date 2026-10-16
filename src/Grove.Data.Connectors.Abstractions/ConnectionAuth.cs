namespace Grove.Data.Connectors;

/// <summary>The auth scheme ids. A protocol lists the ones it carries (<see cref="IConnectorProtocol.AuthSchemes"/>).</summary>
public static class AuthSchemes
{
    public const string None = "none";
    public const string ApiKey = "api-key";
    public const string Basic = "basic";
    public const string Bearer = "bearer";
    public const string OAuth2ClientCredentials = "oauth2-client-credentials";
    public const string OAuth2AuthorizationCode = "oauth2-authorization-code";
    public const string OAuth2DeviceCode = "oauth2-device-code";

    /// <summary>Inbound only: a shared secret that signs webhook bodies; such a connection makes no calls.</summary>
    public const string Hmac = "hmac";

    /// <summary>The protocol or SDK applies the credential itself (a SQL connection string, an SDK client).</summary>
    public const string Custom = "custom";

    public static bool IsOAuth2(string scheme) =>
        scheme is OAuth2ClientCredentials or OAuth2AuthorizationCode or OAuth2DeviceCode;

    /// <summary>Schemes whose credential is a grant the user (or an admin) obtains interactively.</summary>
    public static bool IsInteractive(string scheme) => scheme is OAuth2AuthorizationCode or OAuth2DeviceCode;
}

/// <summary>Where an API key travels.</summary>
public enum ApiKeyLocation
{
    Header,
    Query,
}

/// <summary>
/// A connection's auth scheme and its non-secret settings. The secrets (key, password,
/// client secret, tokens) are the credential's (<see cref="ConnectionCredential"/>).
/// </summary>
public sealed record ConnectionAuth
{
    public static readonly ConnectionAuth None = new() { Scheme = AuthSchemes.None };

    public required string Scheme { get; init; }

    /// <summary>api-key: the header or query parameter name (default <c>X-Api-Key</c>).</summary>
    public string? KeyName { get; init; }

    public ApiKeyLocation KeyLocation { get; init; } = ApiKeyLocation.Header;

    /// <summary>OAuth 2: the authorization endpoint (authorization code).</summary>
    public string? AuthorizeUrl { get; init; }

    /// <summary>OAuth 2: the token endpoint.</summary>
    public string? TokenUrl { get; init; }

    /// <summary>OAuth 2: the device authorization endpoint (device code).</summary>
    public string? DeviceAuthorizationUrl { get; init; }

    /// <summary>OAuth 2: the revocation endpoint (RFC 7009), when the provider has one.</summary>
    public string? RevokeUrl { get; init; }

    /// <summary>OAuth 2: the scopes requested.</summary>
    public IReadOnlyList<string> Scopes { get; init; } = [];

    /// <summary>OAuth 2 authorization code: send a PKCE challenge (S256). On by default.</summary>
    public bool UsePkce { get; init; } = true;

    /// <summary>
    /// OAuth 2: the key of the app credentials (client id and secret, tenant scope) this
    /// connection shares with a sign-in provider; null when the connection's own tenant
    /// credential holds them. See <see cref="IAppCredentialsProvider"/>.
    /// </summary>
    public string? AppCredentialsKey { get; init; }

    /// <summary>hmac: the signature scheme id (<see cref="IWebhookSignatureScheme.Id"/>) and its header.</summary>
    public string? SignatureScheme { get; init; }

    public string? SignatureHeader { get; init; }

    /// <summary>Further scheme settings (a token audience, a basic user name default, extra authorize parameters).</summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
