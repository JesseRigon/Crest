using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Deployment;
using Grove.Display.Management.Descriptors;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Display.Placements.Deployment;
using Grove.Display.Placements.Recipes;
using Grove.Display.Placements.Services;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;
using Grove.Api;

namespace Grove.Display.Placements;

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

        services.AddApiContributor<Api.PlacementsApi>();
    }
}

[Feature("Grove.Display.Placements.FileStorage")]
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
