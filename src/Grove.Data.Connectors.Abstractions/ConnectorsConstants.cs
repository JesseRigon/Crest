using Grove.Security.Permissions;

namespace Grove.Data.Connectors;

/// <summary>Routes, configuration and data-protection names of the connector engine.</summary>
public static class ConnectorsConstants
{
    /// <summary>The feature (and module) id.</summary>
    public const string FeatureId = "Grove.Data.Connectors";

    /// <summary>The API root: connections, credentials, operations, OAuth (admin shell only).</summary>
    public const string ApiRoute = "api/grove/connections";

    /// <summary>The OAuth callback every authorization-code connection registers with its provider.</summary>
    public const string OAuthCallbackRoute = ApiRoute + "/oauth/callback";

    /// <summary>The Connections screen (admin shell).</summary>
    public const string ConnectionsPage = "/connections";

    /// <summary>The caller's own Connected accounts page (admin shell).</summary>
    public const string ConnectedAccountsPage = "/connected-accounts";

    /// <summary>The data-protection purpose credentials are sealed under.</summary>
    public const string CredentialsProtectionPurpose = "Connectors.Credentials";

    /// <summary>The data-protection purpose of the OAuth state.</summary>
    public const string OAuthStateProtectionPurpose = "Connectors.OAuthState";
}

/// <summary>Connection settings the engine itself reads.</summary>
public static class ConnectionSettingKeys
{
    /// <summary>
    /// Pins the connection's TLS server: SHA-256 fingerprints (hex, comma-separated) of the leaf
    /// certificates it accepts. When set, a matching certificate is accepted even when its chain
    /// is not trusted (a self-signed search cluster), and any other is refused.
    /// </summary>
    public const string CertificateFingerprint = "tls:certificateFingerprint";

    /// <summary>The Elasticsearch integration's name for <see cref="CertificateFingerprint"/>, honoured as an alias.</summary>
    public const string ElasticsearchFingerprint = "es:certificateFingerprint";
}

/// <summary>Per-shell engine options, from the shell configuration section <c>Connectors</c>.</summary>
public sealed class ConnectorOptions
{
    public const string ConfigurationSection = "Connectors";

    /// <summary>
    /// Allow calls to loopback, private, link-local and CGNAT addresses. Off by default (SSRF);
    /// a host's dev setup turns it on to call a local endpoint.
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }

    /// <summary>The ceiling on any connection's response cap.</summary>
    public long MaxResponseBytes { get; set; } = 100 * 1024 * 1024;

    public long MaxWebhookBytes { get; set; } = 1024 * 1024;

    /// <summary>How long an OAuth state stays valid.</summary>
    public TimeSpan OAuthStateLifetime { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>The permission names, as constants for attributes (<c>[RequiresPermission]</c>, page entries).</summary>
public static class ConnectorsPermissionNames
{
    public const string ManageConnections = "ManageConnections";
    public const string UseConnections = "UseConnections";
    public const string ConnectOwnAccounts = "ConnectOwnAccounts";
}

public static class ConnectorsPermissions
{
    /// <summary>Define connections, manage tenant credentials, import schemas, revoke any user's grant (never read it).</summary>
    public static readonly Permission ManageConnections = new(ConnectorsPermissionNames.ManageConnections, "Manage connections and their tenant credentials", isSecurityCritical: true);

    /// <summary>Run a connection's operations (from a flow, a query, a component); implied by ManageConnections.</summary>
    public static readonly Permission UseConnections = new(ConnectorsPermissionNames.UseConnections, "Use connections: run their operations", [ManageConnections]);

    /// <summary>Connect one's own accounts (user-scope credentials and OAuth grants) on the Connected accounts page. Staff only.</summary>
    public static readonly Permission ConnectOwnAccounts = new(ConnectorsPermissionNames.ConnectOwnAccounts, "Connect your own accounts to connections", [ManageConnections]);

    public static readonly IReadOnlyList<Permission> All = [ManageConnections, UseConnections, ConnectOwnAccounts];
}
