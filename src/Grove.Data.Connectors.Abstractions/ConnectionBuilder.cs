using System.Reflection;

namespace Grove.Data.Connectors;

/// <summary>Builds a declared <see cref="ConnectionDefinition"/>; used by <c>services.AddConnection(key, builder =&gt; ...)</c>.</summary>
public sealed class ConnectionBuilder
{
    private readonly string _key;
    private string? _displayName;
    private string? _description;
    private string _protocol = ConnectorProtocols.Http;
    private string? _featureId;
    private string? _baseUrl;
    private readonly Dictionary<string, string> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _settings = new(StringComparer.OrdinalIgnoreCase);
    private ConnectionSchemaSource? _schema;
    private ConnectionAuth _auth = ConnectionAuth.None;
    private readonly List<CredentialScope> _scopes = [];
    private ConnectionPolicy _policy = new();
    private readonly List<ConnectionOperation> _operations = [];
    private readonly List<string> _callerRoles = [];

    public ConnectionBuilder(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        _key = key;
    }

    public ConnectionBuilder DisplayName(string displayName, string? description = null)
    {
        _displayName = displayName;
        _description = description ?? _description;
        return this;
    }

    public ConnectionBuilder Protocol(string protocol)
    {
        _protocol = protocol;
        return this;
    }

    /// <summary>The feature that declares the connection (shown on the Connections screen).</summary>
    public ConnectionBuilder Feature(string featureId)
    {
        _featureId = featureId;
        return this;
    }

    public ConnectionBuilder BaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl;
        return this;
    }

    public ConnectionBuilder Endpoint(string name, string url)
    {
        _endpoints[name] = url;
        return this;
    }

    public ConnectionBuilder Setting(string name, string value)
    {
        _settings[name] = value;
        return this;
    }

    public ConnectionBuilder Schema(ConnectionSchemaSource schema)
    {
        _schema = schema;
        return this;
    }

    public ConnectionBuilder OpenApi(Assembly assembly, string resourceName) =>
        Schema(ConnectionSchemaSource.Bundled(ConnectionSchemaFormats.OpenApi, assembly, resourceName));

    public ConnectionBuilder OpenApi(string url) => Schema(ConnectionSchemaSource.FromUrl(ConnectionSchemaFormats.OpenApi, url));

    public ConnectionBuilder Auth(ConnectionAuth auth)
    {
        _auth = auth;
        return this;
    }

    public ConnectionBuilder ApiKey(string keyName = "X-Api-Key", ApiKeyLocation location = ApiKeyLocation.Header) =>
        Auth(new ConnectionAuth { Scheme = AuthSchemes.ApiKey, KeyName = keyName, KeyLocation = location });

    public ConnectionBuilder Basic() => Auth(new ConnectionAuth { Scheme = AuthSchemes.Basic });

    public ConnectionBuilder Bearer() => Auth(new ConnectionAuth { Scheme = AuthSchemes.Bearer });

    public ConnectionBuilder OAuth2AuthorizationCode(string authorizeUrl, string tokenUrl, IEnumerable<string>? scopes = null, string? revokeUrl = null, string? appCredentialsKey = null, bool usePkce = true) =>
        Auth(new ConnectionAuth
        {
            Scheme = AuthSchemes.OAuth2AuthorizationCode,
            AuthorizeUrl = authorizeUrl,
            TokenUrl = tokenUrl,
            RevokeUrl = revokeUrl,
            Scopes = scopes?.ToArray() ?? [],
            AppCredentialsKey = appCredentialsKey,
            UsePkce = usePkce,
        });

    public ConnectionBuilder OAuth2ClientCredentials(string tokenUrl, IEnumerable<string>? scopes = null, string? appCredentialsKey = null) =>
        Auth(new ConnectionAuth
        {
            Scheme = AuthSchemes.OAuth2ClientCredentials,
            TokenUrl = tokenUrl,
            Scopes = scopes?.ToArray() ?? [],
            AppCredentialsKey = appCredentialsKey,
        });

    public ConnectionBuilder OAuth2DeviceCode(string deviceAuthorizationUrl, string tokenUrl, IEnumerable<string>? scopes = null, string? appCredentialsKey = null) =>
        Auth(new ConnectionAuth
        {
            Scheme = AuthSchemes.OAuth2DeviceCode,
            DeviceAuthorizationUrl = deviceAuthorizationUrl,
            TokenUrl = tokenUrl,
            Scopes = scopes?.ToArray() ?? [],
            AppCredentialsKey = appCredentialsKey,
        });

    public ConnectionBuilder Hmac(string signatureHeader = "X-Signature", string scheme = "hmac-sha256") =>
        Auth(new ConnectionAuth { Scheme = AuthSchemes.Hmac, SignatureHeader = signatureHeader, SignatureScheme = scheme });

    /// <summary>The credential scopes allowed; the first is the default. Without a call, tenant only.</summary>
    public ConnectionBuilder AllowScopes(params CredentialScope[] scopes)
    {
        _scopes.Clear();
        _scopes.AddRange(scopes.Distinct());
        return this;
    }

    public ConnectionBuilder Policy(Func<ConnectionPolicy, ConnectionPolicy> configure)
    {
        _policy = configure(_policy);
        return this;
    }

    public ConnectionBuilder Operation(ConnectionOperation operation)
    {
        _operations.Add(operation);
        return this;
    }

    public ConnectionBuilder CallerRoles(params string[] roles)
    {
        _callerRoles.AddRange(roles);
        return this;
    }

    public ConnectionDefinition Build() => new()
    {
        Key = _key,
        DisplayName = _displayName ?? _key,
        Description = _description,
        Protocol = _protocol,
        Origin = ConnectionOrigin.Declared,
        FeatureId = _featureId,
        BaseUrl = _baseUrl,
        Endpoints = new Dictionary<string, string>(_endpoints, StringComparer.OrdinalIgnoreCase),
        Settings = new Dictionary<string, string>(_settings, StringComparer.OrdinalIgnoreCase),
        Schema = _schema,
        Auth = _auth,
        AllowedScopes = _scopes.Count > 0 ? [.. _scopes] : [CredentialScope.Tenant],
        Policy = _policy,
        Operations = [.. _operations],
        CallerRoles = [.. _callerRoles],
        Version = $"declared:{_key}",
    };
}
