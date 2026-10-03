using Crest.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Navigation;

namespace Crest.Workflows.Navigation;

/// <summary>
/// The Workflows admin root: definitions (list and designer) and instances, the forked
/// Studio mounted in the Crest shell. Replaces the stock module's "Workflows" entry, which
/// CoreStartup removes (its workflow types are not run in this tenant).
/// </summary>
public sealed class WorkflowsAdminMenu(IStringLocalizer<WorkflowsAdminMenu> stringLocalizer, IOptions<AdminOptions> adminOptions) : AdminNavigationProvider
{
    private readonly IStringLocalizer S = stringLocalizer;

    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        var adminPath = "/" + adminOptions.Value.AdminUrlPrefix.Trim('/');

        builder.Add(S["Workflows"], "70", workflows =>
        {
            workflows
                .AddClass("workflows")
                .AddClass("icon-class-fa fa-project-diagram")
                .Id("workflows")
                .Url($"{adminPath}{WorkflowsConstants.Routes.ApprovalsAdmin}")
                // Any admin user may have approvals to decide; the pages below take their own
                // permissions, so the root is shown to everyone who can reach one.
                .Permission(AdminPermissions.AccessAdminPanel);

            workflows.Add(S["Definitions"], "0", item => item
                .Id("workflows-definitions")
                .Url($"{adminPath}{WorkflowsConstants.Routes.DefinitionsAdmin}")
                .Permission(Permissions.ViewWorkflows)
                .LocalNav());

            workflows.Add(S["Instances"], "10", item => item
                .Id("workflows-instances")
                .Url($"{adminPath}{WorkflowsConstants.Routes.InstancesAdmin}")
                .Permission(Permissions.ViewWorkflows)
                .LocalNav());

            workflows.Add(S["Connections"], "20", item => item
                .Id("workflows-connections")
                .Url($"{adminPath}{WorkflowsConstants.Routes.ConnectionsAdmin}")
                .Permission(Permissions.ManageConnections)
                .LocalNav());

            workflows.Add(S["Approvals"], "30", item => item
                .Id("workflows-approvals")
                .Url($"{adminPath}{WorkflowsConstants.Routes.ApprovalsAdmin}")
                .Permission(AdminPermissions.AccessAdminPanel)
                .LocalNav());
        });

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The Workflows pages take the same permissions as the APIs behind them (View for the
/// Studio pages, whose engine API gate then grants per permission; Manage connections for
/// Connections), and they run in the browser only: Studio's services and HTTP clients are
/// registered in the WASM client (Crest.Workflows.BlazorWasm), never in a server circuit.
/// </summary>
public sealed class WorkflowsRoutePermissionProvider : ICrestRoutePermissionProvider, ICrestWebAssemblyRouteProvider
{
    // The Studio pages only; Connections and Approvals are plain Crest pages.
    public IEnumerable<string> GetWebAssemblyRoutes() =>
    [
        WorkflowsConstants.Routes.DefinitionsAdmin,
        WorkflowsConstants.Routes.DefinitionEditAdmin,
        WorkflowsConstants.Routes.InstancesAdmin,
        WorkflowsConstants.Routes.InstanceViewAdmin,
    ];

    public IEnumerable<CrestRoutePermission> GetRoutes() =>
    [
        new(WorkflowsConstants.Routes.DefinitionsAdmin, Permissions.ViewWorkflows),
        new(WorkflowsConstants.Routes.DefinitionEditAdmin, Permissions.ViewWorkflows),
        new(WorkflowsConstants.Routes.InstancesAdmin, Permissions.ViewWorkflows),
        new(WorkflowsConstants.Routes.InstanceViewAdmin, Permissions.ViewWorkflows),
        new(WorkflowsConstants.Routes.ConnectionsAdmin, Permissions.ManageConnections),
        new(WorkflowsConstants.Routes.ApprovalsAdmin, AdminPermissions.AccessAdminPanel),
    ];
}
