namespace Grove.Data.Connectors;

/// <summary>Where a connection comes from: the registry's two layers.</summary>
public enum ConnectionOrigin
{
    /// <summary>Declared in code by a module (<c>services.AddConnection(...)</c>).</summary>
    Declared,

    /// <summary>Defined by a tenant admin as data, with no module behind it.</summary>
    Defined,
}

/// <summary>
/// A handle on one external service: its protocol, endpoints, schema, auth scheme, allowed
/// credential scopes and policy. Holds no secret: credentials live in the
/// <see cref="ICredentialStore"/>, keyed by <see cref="Key"/> and scope.
/// </summary>
public sealed class ConnectionDefinition
{
    /// <summary>The tenant-unique key (lowercase, dashes): <c>facebook-graph</c>, <c>crm</c>.</summary>
    public required string Key { get; init; }

    public required string DisplayName { get; init; }

    public string? Description { get; init; }

    /// <summary>The <see cref="IConnectorProtocol.Id"/> that runs the connection (<see cref="ConnectorProtocols"/>).</summary>
    public required string Protocol { get; init; }

    public ConnectionOrigin Origin { get; init; } = ConnectionOrigin.Defined;

    /// <summary>The feature that declared it; null for a defined connection.</summary>
    public string? FeatureId { get; init; }

    /// <summary>The base URL every operation's path resolves under, and may not leave. For SQL, null (the endpoint is in <see cref="Settings"/>).</summary>
    public string? BaseUrl { get; init; }

    /// <summary>Other named endpoints the protocol uses (a token URL, an upload host, a SOAP endpoint per port).</summary>
    public IReadOnlyDictionary<string, string> Endpoints { get; init; } = Empty;

    /// <summary>Where the operations come from; null when the operations are listed in code or the connection is called by raw request only.</summary>
    public ConnectionSchemaSource? Schema { get; init; }

    public ConnectionAuth Auth { get; init; } = ConnectionAuth.None;

    /// <summary>The credential scopes the connection accepts; the first is the default.</summary>
    public IReadOnlyList<CredentialScope> AllowedScopes { get; init; } = [CredentialScope.Tenant];

    public ConnectionPolicy Policy { get; init; } = new();

    /// <summary>Operations declared in code (beside or instead of a schema's).</summary>
    public IReadOnlyList<ConnectionOperation> Operations { get; init; } = [];

    /// <summary>Non-secret protocol settings (a SQL provider name and database, a SOAP binding, a GraphQL path).</summary>
    public IReadOnlyDictionary<string, string> Settings { get; init; } = Empty;

    /// <summary>
    /// Roles the connection's application caller holds (an inbound webhook runs as it, name
    /// identifier <c>connection:{key}</c>); read from here on every build.
    /// </summary>
    public IReadOnlyList<string> CallerRoles { get; init; } = [];

    /// <summary>Changes whenever the definition or its credentials change; keys caches (tokens, operations, rate limiters).</summary>
    public string Version { get; init; } = string.Empty;

    public string? Setting(string name) =>
        Settings.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public bool Allows(CredentialScope scope) => AllowedScopes.Contains(scope);

    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>The protocol ids Grove ships.</summary>
public static class ConnectorProtocols
{
    public const string Http = "http";
    public const string Rest = "rest";
    public const string Soap = "soap";
    public const string GraphQL = "graphql";
    public const string Sql = "sql";
    public const string Webhook = "webhook";
    public const string Smtp = "smtp";
    public const string Imap = "imap";
    public const string Pop = "pop";

    /// <summary>A vendor SDK called through <see cref="IConnectorEngine.RunSdkAsync{T}"/>.</summary>
    public const string Sdk = "sdk";
}

/// <summary>The schema formats a connection may carry.</summary>
public static class ConnectionSchemaFormats
{
    public const string OpenApi = "openapi";
    public const string Wsdl = "wsdl";
    public const string GraphQL = "graphql";
}

public enum ConnectionSchemaSourceKind
{
    /// <summary>An embedded resource of the declaring module's assembly.</summary>
    Bundled,

    /// <summary>Fetched from a URL (through the guarded client) and cached with the definition's version.</summary>
    Url,

    /// <summary>The document itself, stored on a defined connection (an imported file).</summary>
    Inline,
}

/// <summary>Where a connection's schema comes from, and in which format.</summary>
public sealed record ConnectionSchemaSource(ConnectionSchemaSourceKind Kind, string Format, string Location, string? Content = null, string? AssemblyName = null)
{
    public static ConnectionSchemaSource Bundled(string format, System.Reflection.Assembly assembly, string resourceName) =>
        new(ConnectionSchemaSourceKind.Bundled, format, resourceName, AssemblyName: assembly.GetName().Name);

    public static ConnectionSchemaSource FromUrl(string format, string url) => new(ConnectionSchemaSourceKind.Url, format, url);

    public static ConnectionSchemaSource Inline(string format, string content, string? fileName = null) =>
        new(ConnectionSchemaSourceKind.Inline, format, fileName ?? string.Empty, content);
}

/// <summary>A schema document read from its source, handed to <see cref="IConnectorProtocol.ReadOperationsAsync"/>.</summary>
public sealed record ConnectionSchemaDocument(string Format, string Content, string? Location = null);
