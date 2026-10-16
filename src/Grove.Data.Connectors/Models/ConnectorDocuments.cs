using Grove.Data.Documents;

namespace Grove.Data.Connectors.Models;

/// <summary>The tenant's defined connections: one document, few entries, cached.</summary>
public sealed class DefinedConnectionsDocument : Document
{
    public Dictionary<string, DefinedConnectionRecord> Connections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A defined connection as stored (the mutable twin of <see cref="ConnectionDefinition"/>).</summary>
public sealed class DefinedConnectionRecord
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Protocol { get; set; } = ConnectorProtocols.Http;
    public string? BaseUrl { get; set; }
    public Dictionary<string, string> Endpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ConnectionSchemaSourceKind? SchemaKind { get; set; }
    public string? SchemaFormat { get; set; }
    public string? SchemaLocation { get; set; }
    public string? SchemaContent { get; set; }
    public ConnectionAuthRecord Auth { get; set; } = new();
    public List<string> AllowedScopes { get; set; } = [CredentialScope.Tenant.Value];
    public ConnectionPolicyRecord Policy { get; set; } = new();
    public List<ConnectionOperation> Operations { get; set; } = [];
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> CallerRoles { get; set; } = [];
    public DateTime UpdatedUtc { get; set; }

    public ConnectionDefinition ToDefinition() => new()
    {
        Key = Key,
        DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? Key : DisplayName,
        Description = Description,
        Protocol = Protocol,
        Origin = ConnectionOrigin.Defined,
        BaseUrl = BaseUrl,
        Endpoints = new Dictionary<string, string>(Endpoints, StringComparer.OrdinalIgnoreCase),
        Schema = SchemaKind is { } kind && SchemaFormat is { } format
            ? new ConnectionSchemaSource(kind, format, SchemaLocation ?? string.Empty, SchemaContent)
            : null,
        Auth = Auth.ToAuth(),
        AllowedScopes = AllowedScopes.Count == 0 ? [CredentialScope.Tenant] : AllowedScopes.Select(s => new CredentialScope(s)).Distinct().ToArray(),
        Policy = Policy.ToPolicy(),
        Operations = [.. Operations],
        Settings = new Dictionary<string, string>(Settings, StringComparer.OrdinalIgnoreCase),
        CallerRoles = [.. CallerRoles],
        Version = $"defined:{Key}:{UpdatedUtc.Ticks}",
    };

    public static DefinedConnectionRecord From(ConnectionDefinition definition) => new()
    {
        Key = definition.Key,
        DisplayName = definition.DisplayName,
        Description = definition.Description,
        Protocol = definition.Protocol,
        BaseUrl = definition.BaseUrl,
        Endpoints = new Dictionary<string, string>(definition.Endpoints, StringComparer.OrdinalIgnoreCase),
        SchemaKind = definition.Schema?.Kind,
        SchemaFormat = definition.Schema?.Format,
        SchemaLocation = definition.Schema?.Location,
        SchemaContent = definition.Schema?.Content,
        Auth = ConnectionAuthRecord.From(definition.Auth),
        AllowedScopes = definition.AllowedScopes.Select(s => s.Value).ToList(),
        Policy = ConnectionPolicyRecord.From(definition.Policy),
        Operations = [.. definition.Operations],
        Settings = new Dictionary<string, string>(definition.Settings, StringComparer.OrdinalIgnoreCase),
        CallerRoles = [.. definition.CallerRoles],
    };
}

public sealed class ConnectionAuthRecord
{
    public string Scheme { get; set; } = AuthSchemes.None;
    public string? KeyName { get; set; }
    public ApiKeyLocation KeyLocation { get; set; }
    public string? AuthorizeUrl { get; set; }
    public string? TokenUrl { get; set; }
    public string? DeviceAuthorizationUrl { get; set; }
    public string? RevokeUrl { get; set; }
    public List<string> Scopes { get; set; } = [];
    public bool UsePkce { get; set; } = true;
    public string? AppCredentialsKey { get; set; }
    public string? SignatureScheme { get; set; }
    public string? SignatureHeader { get; set; }
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ConnectionAuth ToAuth() => new()
    {
        Scheme = Scheme,
        KeyName = KeyName,
        KeyLocation = KeyLocation,
        AuthorizeUrl = AuthorizeUrl,
        TokenUrl = TokenUrl,
        DeviceAuthorizationUrl = DeviceAuthorizationUrl,
        RevokeUrl = RevokeUrl,
        Scopes = [.. Scopes],
        UsePkce = UsePkce,
        AppCredentialsKey = AppCredentialsKey,
        SignatureScheme = SignatureScheme,
        SignatureHeader = SignatureHeader,
        Settings = new Dictionary<string, string>(Settings, StringComparer.OrdinalIgnoreCase),
    };

    public static ConnectionAuthRecord From(ConnectionAuth auth) => new()
    {
        Scheme = auth.Scheme,
        KeyName = auth.KeyName,
        KeyLocation = auth.KeyLocation,
        AuthorizeUrl = auth.AuthorizeUrl,
        TokenUrl = auth.TokenUrl,
        DeviceAuthorizationUrl = auth.DeviceAuthorizationUrl,
        RevokeUrl = auth.RevokeUrl,
        Scopes = [.. auth.Scopes],
        UsePkce = auth.UsePkce,
        AppCredentialsKey = auth.AppCredentialsKey,
        SignatureScheme = auth.SignatureScheme,
        SignatureHeader = auth.SignatureHeader,
        Settings = new Dictionary<string, string>(auth.Settings, StringComparer.OrdinalIgnoreCase),
    };
}

public sealed class ConnectionPolicyRecord
{
    public int RetryCount { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 30;
    public int? RateLimitPermits { get; set; }
    public int? RateLimitWindowSeconds { get; set; }
    public long? QuotaLimit { get; set; }
    public int? QuotaPeriodHours { get; set; }
    public int? CacheSeconds { get; set; }
    public long MaxResponseBytes { get; set; } = 1024 * 1024;
    public long MaxRequestBytes { get; set; } = 1024 * 1024;
    public int MaxInlineWaitSeconds { get; set; } = 5;
    public bool AllowTenantFallback { get; set; }

    public ConnectionPolicy ToPolicy() => new()
    {
        RetryCount = RetryCount,
        Timeout = TimeSpan.FromSeconds(TimeoutSeconds),
        RateLimit = RateLimitPermits is > 0 ? new RateLimitPolicy(RateLimitPermits.Value, TimeSpan.FromSeconds(RateLimitWindowSeconds is > 0 ? RateLimitWindowSeconds.Value : 60)) : null,
        Quota = QuotaLimit is > 0 ? new QuotaPolicy(QuotaLimit.Value, TimeSpan.FromHours(QuotaPeriodHours is > 0 ? QuotaPeriodHours.Value : 24)) : null,
        CacheLifetime = CacheSeconds is > 0 ? TimeSpan.FromSeconds(CacheSeconds.Value) : null,
        MaxResponseBytes = MaxResponseBytes,
        MaxRequestBytes = MaxRequestBytes,
        MaxInlineWait = TimeSpan.FromSeconds(MaxInlineWaitSeconds),
        AllowTenantFallback = AllowTenantFallback,
    };

    public static ConnectionPolicyRecord From(ConnectionPolicy policy) => new()
    {
        RetryCount = policy.RetryCount,
        TimeoutSeconds = (int)policy.Timeout.TotalSeconds,
        RateLimitPermits = policy.RateLimit?.Permits,
        RateLimitWindowSeconds = policy.RateLimit is { } rate ? (int)rate.Window.TotalSeconds : null,
        QuotaLimit = policy.Quota?.Limit,
        QuotaPeriodHours = policy.Quota is { } quota ? (int)Math.Max(1, quota.Period.TotalHours) : null,
        CacheSeconds = policy.CacheLifetime is { } cache ? (int)cache.TotalSeconds : null,
        MaxResponseBytes = policy.MaxResponseBytes,
        MaxRequestBytes = policy.MaxRequestBytes,
        MaxInlineWaitSeconds = (int)policy.MaxInlineWait.TotalSeconds,
        AllowTenantFallback = policy.AllowTenantFallback,
    };
}

/// <summary>Every sealed credential and app credential set of the tenant.</summary>
public sealed class ConnectorCredentialsDocument : Document
{
    /// <summary>Keyed <c>{connection}|{scope}|{owner}</c>.</summary>
    public Dictionary<string, StoredCredential> Credentials { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, StoredAppCredentials> AppCredentials { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static string KeyOf(string connectionKey, CredentialOwner owner) => $"{connectionKey}|{owner.Scope.Value}|{owner.OwnerId}";
}

public sealed class StoredCredential
{
    public string ConnectionKey { get; set; } = string.Empty;
    public string Scope { get; set; } = CredentialScope.Tenant.Value;
    public string? OwnerId { get; set; }
    public string? Label { get; set; }
    public string? AccountName { get; set; }
    public List<string> ValueNames { get; set; } = [];
    public List<string> GrantedScopes { get; set; } = [];

    /// <summary>The values, sealed (a JSON object).</summary>
    public string? ProtectedValues { get; set; }

    /// <summary>The OAuth tokens, sealed (an <see cref="OAuthTokenSet"/> as JSON).</summary>
    public string? ProtectedTokens { get; set; }

    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }

    public CredentialOwner Owner => new(new CredentialScope(Scope), OwnerId);
}

public sealed class StoredAppCredentials
{
    public string Key { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string? ProtectedSecret { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
