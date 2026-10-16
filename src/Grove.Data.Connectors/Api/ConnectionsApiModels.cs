namespace Grove.Data.Connectors.Api;

// The wire types of api/grove/connections, shared with the Connections and Connected accounts
// pages (Grove.Data.Connectors.BlazorWasm links this file). Secrets go in, never out: a
// credential is described by the names of the values it holds.

public sealed record ConnectionAuthModel(
    string Scheme,
    string? KeyName = null,
    string KeyLocation = "header",
    string? AuthorizeUrl = null,
    string? TokenUrl = null,
    string? DeviceAuthorizationUrl = null,
    string? RevokeUrl = null,
    IReadOnlyList<string>? Scopes = null,
    bool UsePkce = true,
    string? AppCredentialsKey = null,
    string? SignatureScheme = null,
    string? SignatureHeader = null);

public sealed record ConnectionPolicyModel(
    int RetryCount = 2,
    int TimeoutSeconds = 30,
    int? RateLimitPermits = null,
    int? RateLimitWindowSeconds = null,
    long? QuotaLimit = null,
    int? QuotaPeriodHours = null,
    int? CacheSeconds = null,
    long MaxResponseBytes = 1024 * 1024,
    long MaxRequestBytes = 1024 * 1024,
    int MaxInlineWaitSeconds = 5,
    bool AllowTenantFallback = false);

public sealed record CredentialInfoModel(
    string ConnectionKey,
    string Scope,
    string? OwnerId,
    string? Label,
    string? AccountName,
    IReadOnlyList<string> ValueNames,
    IReadOnlyList<string> GrantedScopes,
    DateTime UpdatedUtc,
    DateTime? ExpiresUtc,
    string Status);

public sealed record ConnectionModel(
    string Key,
    string DisplayName,
    string? Description,
    string Protocol,
    string Origin,
    string? FeatureId,
    string? BaseUrl,
    IReadOnlyDictionary<string, string> Endpoints,
    ConnectionAuthModel Auth,
    IReadOnlyList<string> AllowedScopes,
    ConnectionPolicyModel Policy,
    IReadOnlyDictionary<string, string> Settings,
    IReadOnlyList<string> CallerRoles,
    string? SchemaFormat,
    string? SchemaKind,
    string? SchemaLocation,
    CredentialInfoModel? TenantCredential,
    int UserCredentialCount);

public sealed record OperationModel(
    string Id,
    string? DisplayName,
    string? Description,
    string Kind,
    string? Method,
    string? Path,
    bool IsSafeToRetry,
    bool SupportsIdempotencyKey,
    IReadOnlyList<string> Parameters,
    string? Paging);

public sealed record ConnectionDetailModel(
    ConnectionModel Connection,
    IReadOnlyList<OperationModel> Operations,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<CredentialInfoModel> UserCredentials);

/// <summary>An operation entered by hand on a defined connection.</summary>
public sealed record OperationSaveModel(string Id, string? DisplayName, string Kind, string? Method, string? Path, bool IsSafeToRetry, bool SupportsIdempotencyKey = false);

/// <summary>Create or update a defined connection. Null members keep the stored value on update.</summary>
public sealed class ConnectionSaveRequest
{
    public string? Key { get; set; }
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Protocol { get; set; }
    public string? BaseUrl { get; set; }
    public Dictionary<string, string>? Endpoints { get; set; }
    public ConnectionAuthModel? Auth { get; set; }
    public List<string>? AllowedScopes { get; set; }
    public ConnectionPolicyModel? Policy { get; set; }
    public Dictionary<string, string>? Settings { get; set; }
    public List<string>? CallerRoles { get; set; }
    public List<OperationSaveModel>? Operations { get; set; }
}

/// <summary>A defined connection from a schema: an OpenAPI document, a WSDL or a GraphQL schema, given inline or by URL.</summary>
public sealed class SchemaImportRequest
{
    public string? Key { get; set; }
    public string? DisplayName { get; set; }

    /// <summary>openapi, wsdl or graphql.</summary>
    public string? Format { get; set; }

    public string? Document { get; set; }
    public string? Url { get; set; }

    /// <summary>For an existing defined connection: replace its schema instead of creating one.</summary>
    public bool Replace { get; set; }
}

public sealed record SchemaImportResult(ConnectionDetailModel Connection, IReadOnlyList<string> Warnings);

/// <summary>Credential values by name (apiKey, username, password, token, clientId, clientSecret, signingSecret, connectionString). An empty value keeps the stored one.</summary>
public sealed class CredentialSaveRequest
{
    public Dictionary<string, string> Values { get; set; } = [];
    public string? Label { get; set; }
}

/// <summary>A protocol and what its connections take: the auth schemes, schema formats, settings and credential fields it describes.</summary>
public sealed record ProtocolModel(
    string Id,
    string DisplayName,
    IReadOnlyList<string> AuthSchemes,
    IReadOnlyList<string> SchemaFormats,
    IReadOnlyList<SettingFieldModel> SettingFields,
    IReadOnlyList<SettingFieldModel> CredentialFields,
    int DefaultOperationCount);

public sealed record SettingFieldModel(string Name, string Label, string Type, bool Required, string? Default, string? Description, IReadOnlyList<string>? Options);

public sealed record AppCredentialsModel(string Key, string? ClientId, bool HasSecret, DateTime? UpdatedUtc, IReadOnlyList<string> UsedBy);

public sealed class AppCredentialsSaveRequest
{
    public string? ClientId { get; set; }

    /// <summary>Null keeps the stored secret, empty clears it.</summary>
    public string? ClientSecret { get; set; }
}

/// <summary>A connection the caller may connect their own account to, with their credential if any.</summary>
public sealed record ConnectedAccountModel(string ConnectionKey, string DisplayName, string? Description, string AuthScheme, bool Interactive, bool TenantFallback, CredentialInfoModel? Credential);

public sealed record AuthorizationStartModel(string Url);

public sealed record DeviceAuthorizationModel(string DeviceCode, string UserCode, string VerificationUri, string? VerificationUriComplete, DateTime ExpiresUtc, int IntervalSeconds);

public sealed class DevicePollRequest
{
    public string? DeviceCode { get; set; }
}

public sealed record DevicePollResult(bool Connected, bool Pending, string? Error);
