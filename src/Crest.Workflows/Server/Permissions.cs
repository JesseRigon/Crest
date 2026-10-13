using Crest.Admin;
using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell;
using Crest.Security;
using Crest.Security.Permissions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows;

/// <summary>
/// The workflow permission set (docs/workflows.md, phase 5). <c>ManageWorkflows</c> stays as
/// the umbrella that implies every one below, so roles configured for workflows carry over. Each finer permission is what a page, a controller or an engine
/// endpoint asks for; the engine API gate maps them onto the engine's own per-endpoint
/// permission names (<see cref="Security.ApiSecurityMiddleware"/>). At run
/// time a flow acts as the caller who started it (or as the system when published so), and
/// its data activities ask the access gate with that caller (docs/operations.md step 4); a
/// definition may narrow who may edit or run it with its own access lists, which are
/// evaluated inside these permissions, not beside them.
/// </summary>
public static class Permissions
{
    public static readonly Permission ManageWorkflows = new("ManageWorkflows", "Manage workflows", isSecurityCritical: true);

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
    // roles of its own, created with the feature (WorkflowRolesFeatureEvents): authors of
    // tenant flows, and readers.
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new() { Name = PlatformConstants.Roles.Administrator, Permissions = [Permissions.ManageWorkflows] },
        new() { Name = PlatformConstants.Roles.Editor, Permissions = [Permissions.ManageWorkflows] },
        .. WorkflowRolesFeatureEvents.Roles.Select(role => new PermissionStereotype { Name = role.Name, Permissions = role.Permissions }),
    ];
}

/// <summary>
/// Ships the two workflow roles with the feature: when Crest.Workflows is installed on a
/// tenant, <c>WorkflowEditor</c> (authors the tenant's own flows: view, edit, publish, run) and
/// <c>WorkflowViewer</c> (reads definitions, instances and journals) are created if the tenant
/// has no role of that name, holding the admin panel and exactly their stereotype's workflow
/// permissions. Every host gets them, so the ownership checks run without a host recipe; a
/// role the tenant already has is left as it is.
/// </summary>
public sealed class WorkflowRolesFeatureEvents(IServiceProvider services) : FeatureEventHandler
{
    public static readonly IReadOnlyList<(string Name, string Description, Permission[] Permissions)> Roles =
    [
        (WorkflowsConstants.Roles.WorkflowEditor, "Authors the tenant's own workflows: view, edit, publish and run; not the shipped templates or connections.",
            [Permissions.ViewWorkflows, Permissions.EditWorkflows, Permissions.PublishWorkflows, Permissions.RunWorkflows]),
        (WorkflowsConstants.Roles.WorkflowViewer, "Reads workflow definitions, instances and journals; changes nothing.",
            [Permissions.ViewWorkflows]),
    ];

    public override async Task InstalledAsync(IFeatureInfo feature)
    {
        if (!string.Equals(feature.Id, WorkflowsConstants.FeatureId, StringComparison.Ordinal)
            || services.GetService<RoleManager<IRole>>() is not { } roleManager)
        {
            return;
        }

        foreach (var (name, description, permissions) in Roles)
        {
            if (await roleManager.FindByNameAsync(name) is not null)
            {
                continue;
            }

            var role = new Role { RoleName = name, RoleDescription = description };
            role.RoleClaims.Add(RoleClaim.Create(AdminPermissions.AccessAdminPanel.Name));
            role.RoleClaims.AddRange(permissions.Select(permission => RoleClaim.Create(permission.Name)));
            await roleManager.CreateAsync(role);
        }
    }
}
