using Microsoft.AspNetCore.Authorization;

namespace Grove.Workflows.Api.Endpoints;

/// <summary>
/// The engine API's authorization policies: one per set of engine permission names, satisfied
/// by a <see cref="PermissionsClaimType"/> claim on the principal that names any of them or the
/// wildcard <see cref="AllPermissions"/>. The host maps its own permissions onto these names per
/// request (Grove.Workflows' <c>ApiSecurityMiddleware</c>); each endpoint declares its policy
/// with <c>RequireAuthorization</c>.
/// </summary>
public static class EngineApiPolicies
{
    public const string PermissionsClaimType = "permissions";

    public const string AllPermissions = "*";

    private const string Prefix = "workflows-engine:";

    // Declared before the policies: their initializers register into it.
    private static readonly Dictionary<string, string[]> s_sets = new();

    public static readonly string ReadActivityDescriptors = Named("read:*", "read:activity-descriptors");
    public static readonly string ReadActivityDescriptorOptions = Named("read:*", "read:activity-descriptors-options");
    public static readonly string ReadActivityExecution = Named("read:activity-execution");
    public static readonly string ReadCommitStrategies = Named("read:commit-strategies");
    public static readonly string ReadIncidentStrategies = Named("read:incident-strategies");
    public static readonly string ReadLogPersistenceStrategies = Named("read:log-persistence-strategies");
    public static readonly string ReadWorkflowActivationStrategies = Named("read:workflow-activation-strategies");
    public static readonly string ReadStorageDrivers = Named("read:*", "read:storage-drivers");
    public static readonly string ReadVariableDescriptors = Named("read:*", "read:variable-descriptors");
    public static readonly string ReadExpressionDescriptors = Named("read:*", "read:expression-descriptors");
    public static readonly string ReadJavaScriptTypeDefinitions = Named("read:*", "read:javascript-type-definitions");
    public static readonly string ReadInstalledFeatures = Named("read:*", "read:installed-features");
    public static readonly string ReadResilienceStrategies = Named("read:*", "read:resilience", "read:resilience:strategies");
    public static readonly string ReadResilienceRetries = Named("read:*", "read:resilience", "read:resilience:retries");
    public static readonly string ReadWorkflowDefinitions = Named("read:workflow-definitions");
    public static readonly string WriteWorkflowDefinitions = Named("write:workflow-definitions");
    public static readonly string DeleteWorkflowDefinitions = Named("delete:workflow-definitions");
    public static readonly string PublishWorkflowDefinitions = Named("publish:workflow-definitions");
    public static readonly string RetractWorkflowDefinitions = Named("retract:workflow-definitions");
    public static readonly string ExecuteWorkflowDefinitions = Named("exec:workflow-definitions");
    public static readonly string RefreshWorkflowDefinitions = Named("actions:workflow-definitions:refresh");
    public static readonly string ReloadWorkflowDefinitions = Named("actions:workflow-definitions:reload");
    public static readonly string ExecuteTests = Named("exec:tests");
    public static readonly string TriggerEvents = Named("trigger:event");
    public static readonly string CompleteTasks = Named("tasks:complete");
    public static readonly string ReadWorkflowInstances = Named("read:workflow-instances");
    public static readonly string WriteWorkflowInstances = Named("write:workflow-instances");
    public static readonly string CancelWorkflowInstances = Named("cancel:workflow-instances");
    public static readonly string DeleteWorkflowInstances = Named("delete:workflow-instances");

    /// <summary>Every policy and the engine permission names that satisfy it.</summary>
    public static IReadOnlyDictionary<string, string[]> All => s_sets;

    /// <summary>Registers every policy with the authorization options.</summary>
    public static void Register(AuthorizationOptions options)
    {
        foreach (var (name, names) in s_sets)
        {
            options.AddPolicy(name, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => context.User.HasClaim(claim =>
                    claim.Type == PermissionsClaimType && (claim.Value == AllPermissions || names.Contains(claim.Value, StringComparer.Ordinal)))));
        }
    }

    private static string Named(params string[] names)
    {
        var name = Prefix + string.Join("|", names);
        s_sets[name] = names;
        return name;
    }
}
