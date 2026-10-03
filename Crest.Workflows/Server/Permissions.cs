using OrchardCore.Security.Permissions;
using OrchardCore.Workflows;

namespace Crest.Workflows;

/// <summary>
/// The workflow permission set (plans/workflows.md, phase 5). The stock module's
/// <c>ManageWorkflows</c> (declared once, by <c>OrchardCore.Workflows</c>, which this feature
/// depends on) stays as the umbrella that implies every one below, so roles configured for
/// workflows carry over. Each finer permission is what a page, a controller or an engine
/// endpoint asks for; the engine API gate maps them onto the engine's own per-endpoint
/// permission names (<see cref="Security.CrestWorkflowsApiSecurityMiddleware"/>). An
/// activity runs as trusted system code once a definition is published, so authoring
/// (Edit + Publish) is the security boundary; a definition may narrow it further with its
/// own access lists, which are evaluated inside these permissions, not beside them.
/// </summary>
public static class Permissions
{
    public static readonly Permission ManageWorkflows = WorkflowsPermissions.ManageWorkflows;

    public static readonly Permission ViewWorkflows = new(WorkflowsConstants.Permissions.View, "View workflows: definitions, instances, journals and the registry", [ManageWorkflows]);
    public static readonly Permission EditWorkflows = new(WorkflowsConstants.Permissions.Edit, "Edit workflows: create and change the tenant's own flows", [ManageWorkflows]);
    public static readonly Permission PublishWorkflows = new(WorkflowsConstants.Permissions.Publish, "Publish and retract workflows", [ManageWorkflows]);
    public static readonly Permission RunWorkflows = new(WorkflowsConstants.Permissions.Run, "Run workflows by hand and manage their instances", [ManageWorkflows]);
    public static readonly Permission ManageShippedWorkflows = new(WorkflowsConstants.Permissions.ManageShipped, "Manage shipped workflows: edit, fork and reset the shipped templates", [ManageWorkflows]);
    public static readonly Permission ManageConnections = new(WorkflowsConstants.Permissions.ManageConnections, "Manage workflow connections and their secrets", [ManageWorkflows]);

    /// <summary>Everything this feature declares (ManageWorkflows is the stock module's).</summary>
    public static readonly IReadOnlyList<Permission> All = [ViewWorkflows, EditWorkflows, PublishWorkflows, RunWorkflows, ManageShippedWorkflows, ManageConnections];
}

public sealed class WorkflowsPermissionProvider : IPermissionProvider
{
    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult<IEnumerable<Permission>>(Permissions.All);

    // Administrator and Editor hold ManageWorkflows from the stock module's stereotypes, so
    // the finer set goes to two roles of its own (shipped by the recipe, defaulted here for
    // a tenant that creates them by hand): authors of tenant flows, and readers.
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new() { Name = WorkflowsConstants.Roles.WorkflowEditor, Permissions = [Permissions.ViewWorkflows, Permissions.EditWorkflows, Permissions.PublishWorkflows, Permissions.RunWorkflows] },
        new() { Name = WorkflowsConstants.Roles.WorkflowViewer, Permissions = [Permissions.ViewWorkflows] },
    ];
}
