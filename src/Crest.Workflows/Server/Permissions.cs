using Crest.Security.Permissions;
using Crest.Workflows.Platform;

namespace Crest.Workflows;

/// <summary>
/// The workflow permission set (docs/workflows.md, phase 5). <c>ManageWorkflows</c> (from the
/// platform activity abstractions, which platform modules check too) stays as the umbrella that implies every one below, so roles configured for
/// workflows carry over. Each finer permission is what a page, a controller or an engine
/// endpoint asks for; the engine API gate maps them onto the engine's own per-endpoint
/// permission names (<see cref="Security.CrestWorkflowsApiSecurityMiddleware"/>). At run
/// time a flow acts as the caller who started it (or as the system when published so), and
/// its data activities ask the access gate with that caller (docs/operations.md step 4); a
/// definition may narrow who may edit or run it with its own access lists, which are
/// evaluated inside these permissions, not beside them.
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
    /// <summary>What the connector activities take at run time, for the burst's caller; managing a connection implies using it.</summary>
    public static readonly Permission UseConnections = new(WorkflowsConstants.Permissions.UseConnections, "Use workflow connections: call and poll them from a running flow", [ManageConnections]);

    /// <summary>Everything this feature declares.</summary>
    public static readonly IReadOnlyList<Permission> All = [ManageWorkflows, ViewWorkflows, EditWorkflows, PublishWorkflows, RunWorkflows, ManageShippedWorkflows, ManageConnections, UseConnections];
}

public sealed class WorkflowsPermissionProvider : IPermissionProvider
{
    public Task<IEnumerable<Permission>> GetPermissionsAsync() => Task.FromResult<IEnumerable<Permission>>(Permissions.All);

    // Administrator and Editor hold the ManageWorkflows umbrella; the finer set goes to two
    // roles of its own (shipped by the recipe, defaulted here for a tenant that creates them
    // by hand): authors of tenant flows, and readers.
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new() { Name = PlatformConstants.Roles.Administrator, Permissions = [Permissions.ManageWorkflows] },
        new() { Name = PlatformConstants.Roles.Editor, Permissions = [Permissions.ManageWorkflows] },
        new() { Name = WorkflowsConstants.Roles.WorkflowEditor, Permissions = [Permissions.ViewWorkflows, Permissions.EditWorkflows, Permissions.PublishWorkflows, Permissions.RunWorkflows] },
        new() { Name = WorkflowsConstants.Roles.WorkflowViewer, Permissions = [Permissions.ViewWorkflows] },
    ];
}
