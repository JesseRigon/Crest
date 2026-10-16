using Grove.Workflows.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.Management;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Handlers;
using Grove.Data.Migration;
using Grove.Display.Management;
using Grove.Data.Localization;
using Grove.Contents.SiteMenu.Handlers;
using Grove.Contents.SiteMenu.Models;
using Grove.Contents.SiteMenu.Services;
using Grove.Contents.SiteMenu.Settings;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;

namespace Grove.Contents.SiteMenu;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // A menu's items render as the Menu shape, not as embedded content items.
        services.Configure<Grove.Contents.Management.EmbeddedContentDisplayOptions>(options =>
            options.DisplayedElsewhere.Add($"Menu.{MenuItemsEmbedding.Name}"));

        services.AddDataMigration<Migrations>();
        services.AddShapeTableProvider<MenuShapes>();
        services.AddPermissionProvider<Permissions>();
        services.AddApiContributor<Api.StandardMenusApi>();

        services.AddScoped<IStereotypesProvider, MenuItemStereotypesProvider>();

        // MenuFieldSet
        services.AddScoped<IContentHandler, MenuContentHandler>();
        services.AddContentFieldSet<MenuFieldSet>();
        services.AddScoped<MenuBehaviour>();

        // LinkMenuItemFieldSet
        services.AddContentFieldSet<LinkMenuItemFieldSet>();

        // ContentMenuItemFieldSet
        services.AddContentFieldSet<ContentMenuItemFieldSet>();

        // HtmlMenuItemFieldSet
        services.AddContentFieldSet<HtmlMenuItemFieldSet>();

        services.AddContentFieldSet<MenuItemPermissionFieldSet>();

    }
}

/// <summary>The field set's system flows and their activities (docs/content-engine.md › Behaviour is workflows).</summary>
[RequireFeatures(Grove.Workflows.WorkflowsConstants.ContentsFeatureId)]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<Grove.Workflows.IWorkflowActivityProvider, Workflows.MenuWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFlowProvider, Workflows.MenuWorkflowProvider>();
        services.AddScoped<Grove.Workflows.IWorkflowFieldSetHookAttachmentProvider, Workflows.MenuWorkflowProvider>();
        services.ConfigureGroveWorkflows(workflows => workflows.AddActivitiesFrom<WorkflowsStartup>());
    }
}
