using OrchardCore.Data.Documents;

namespace Crest.Workflows.Connectors;

/// <summary>
/// A tenant's connection to an external service. The secret is sealed with the tenant's
/// data protection (<see cref="WorkflowConnectionService"/>) and never returned by an API,
/// written into a workflow definition, or exported.
/// </summary>
public sealed class WorkflowConnection
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ConnectorKey { get; set; }
    public string? BaseUrl { get; set; }
    public string AuthKind { get; set; } = WorkflowConnectorAuthKinds.None;
    public Dictionary<string, string> Settings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string? ProtectedSecret { get; set; }
    /// <summary>OAuth2 authorization code: the sealed token set (<see cref="WorkflowConnectionTokens"/> as JSON).</summary>
    public string? ProtectedTokens { get; set; }
    public int RetryCount { get; set; } = 2;
    public int? RateLimitPerMinute { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public DateTime UpdatedUtc { get; set; }

    /// <summary>Changes whenever the connection is saved; keys caches (tokens, rate limiters).</summary>
    public string Version => $"{Key}:{UpdatedUtc.Ticks}";

    public string? Setting(string name) => Settings.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}

/// <summary>The tokens an authorization-code connection holds.</summary>
public sealed record WorkflowConnectionTokens(string AccessToken, string? RefreshToken, DateTime ExpiresUtc);

/// <summary>All of a tenant's connections: one document, few entries, cached by Orchard.</summary>
public sealed class WorkflowConnectionsDocument : Document
{
    public Dictionary<string, WorkflowConnection> Connections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Per-shell connector options, from the shell configuration section <c>CrestWorkflows:Connectors</c>.</summary>
public sealed class WorkflowConnectorOptions
{
    public const string ConfigurationSection = "CrestWorkflows:Connectors";

    /// <summary>
    /// Allow calls to loopback, private, link-local and other non-public addresses. Off by
    /// default: a tenant's connection must not reach the host's own network (SSRF). The dev
    /// and test instances turn it on to call a local endpoint.
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }

    public int MaxResponseBytes { get; set; } = 1024 * 1024;
    public int MaxWebhookBytes { get; set; } = 1024 * 1024;
}
