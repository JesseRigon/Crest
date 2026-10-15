using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.ContentTypes.Editors;
using Grove.Deployment;
using Grove.DisplayManagement.Descriptors;
using Grove.Modules;
using Grove.Navigation;
using Grove.Placements.Deployment;
using Grove.Placements.Recipes;
using Grove.Placements.Services;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.Placements;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<Permissions>();

        services.TryAddScoped<IPlacementStore, DatabasePlacementsStore>();
        services.AddScoped<PlacementsManager>();
        services.AddScoped<IShapePlacementProvider, PlacementProvider>();

        // Recipes
        services.AddRecipeExecutionStep<PlacementStep>();
    }
}

[Feature("Grove.Placements.FileStorage")]
public class FileContentDefinitionStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IPlacementStore>();
        services.AddScoped<IPlacementStore, FilePlacementsStore>();
    }
}

[RequireFeatures("Grove.Deployment")]
public class DeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<PlacementsDeploymentSource, PlacementsDeploymentStep>();
    }
}
