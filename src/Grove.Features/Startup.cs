using Microsoft.Extensions.DependencyInjection;
using Grove.Deployment;
using Grove.Features.Deployment;
using Grove.Features.Recipes.Executors;
using Grove.Features.Services;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Security.Permissions;

namespace Grove.Features;

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

        services.AddDeployment<AllFeaturesDeploymentSource, AllFeaturesDeploymentStep, AllFeaturesDeploymentStepDriver>();
    }
}
