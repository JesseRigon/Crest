using Microsoft.Extensions.DependencyInjection;
using Grove.Deployment;
using Grove.Infrastructure.Features.Deployment;
using Grove.Infrastructure.Features.Recipes.Executors;
using Grove.Infrastructure.Features.Services;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Security.Permissions;

namespace Grove.Infrastructure.Features;

/// <summary>
/// These services are registered on the tenant service collection.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<FeatureStep>();
        services.AddPermissionProvider<Permissions>();
        services.AddScoped<IModuleService, ModuleService>();
        services.AddApiContributor<Api.FeaturesApi>();

        services.AddDeployment<AllFeaturesDeploymentSource, AllFeaturesDeploymentStep>();
    }
}
