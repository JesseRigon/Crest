using Microsoft.Extensions.DependencyInjection;
using Grove.Contents.AdminMenu.AdminNodes;
using Grove.Contents.AdminMenu.Deployment;
using Grove.Contents.AdminMenu.Recipes;
using Grove.Contents.AdminMenu.Services;
using Grove.Data.Migration;
using Grove.Deployment;
using Grove.Data.Localization;
using Grove.Data.Localization.Data;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;

namespace Grove.Contents.AdminMenu;

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

[RequireFeatures("Grove.Data.Localization.Translations")]
public sealed class DataLocalizationStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILocalizationDataProvider, AdminMenuDataLocalizationProvider>();
        services.AddScoped<ILocalizationDataProvider, LinkAdminNodeDataLocalizationProvider>();
        services.AddScoped<ILocalizationDataProvider, PlaceholderAdminNodeDataLocalizationProvider>();
    }
}
