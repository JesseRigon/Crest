using Microsoft.Extensions.DependencyInjection;
using Crest.AdminMenu.AdminNodes;
using Crest.AdminMenu.Deployment;
using Crest.AdminMenu.Recipes;
using Crest.AdminMenu.Services;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.Localization;
using Crest.Localization.Data;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;

namespace Crest.AdminMenu;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<IJSLocalizer, AdminMenuJSLocalizer>();

        services.AddScoped<IAdminMenuService, AdminMenuService>();
        services.AddScoped<IAdminMenuAccessor, AdminMenuAccessor>();
        services.AddScoped<AdminMenuNavigationProvidersCoordinator>();

        services.AddRecipeExecutionStep<AdminMenuStep>();

        services.AddDeployment<AdminMenuDeploymentSource, AdminMenuDeploymentStep, AdminMenuDeploymentStepDriver>();

        // placeholder treeNode
        services.AddAdminNode<PlaceholderAdminNode, PlaceholderAdminNodeNavigationBuilder, PlaceholderAdminNodeDriver>();

        // link treeNode
        services.AddAdminNode<LinkAdminNode, LinkAdminNodeNavigationBuilder, LinkAdminNodeDriver>();

        // Migrate admin menu to the 3.0 format.
        services.AddDataMigration<Migrations>();
    }
}

[RequireFeatures("Crest.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, AdminMenuDataLocalizationProvider>();
        services.AddScoped<ILocalizationDataProvider, LinkAdminNodeDataLocalizationProvider>();
        services.AddScoped<ILocalizationDataProvider, PlaceholderAdminNodeDataLocalizationProvider>();
    }
}
