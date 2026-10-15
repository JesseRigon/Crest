using Microsoft.Extensions.DependencyInjection;
using Grove.AdminMenu.AdminNodes;
using Grove.AdminMenu.Deployment;
using Grove.AdminMenu.Recipes;
using Grove.AdminMenu.Services;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Localization;
using Grove.Localization.Data;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.AdminMenu;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();

        services.AddScoped<IAdminMenuService, AdminMenuService>();
        services.AddScoped<IAdminMenuAccessor, AdminMenuAccessor>();

        services.AddRecipeExecutionStep<AdminMenuStep>();

        services.AddDeployment<AdminMenuDeploymentSource, AdminMenuDeploymentStep>();

        // placeholder treeNode
        services.AddAdminNode<PlaceholderAdminNode, PlaceholderAdminNodeNavigationBuilder>();

        // link treeNode
        services.AddAdminNode<LinkAdminNode, LinkAdminNodeNavigationBuilder>();

        // Migrate admin menu to the 3.0 format.
        services.AddDataMigration<Migrations>();
    }
}

[RequireFeatures("Grove.DataLocalization")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, AdminMenuDataLocalizationProvider>();
        services.AddScoped<ILocalizationDataProvider, LinkAdminNodeDataLocalizationProvider>();
        services.AddScoped<ILocalizationDataProvider, PlaceholderAdminNodeDataLocalizationProvider>();
    }
}
