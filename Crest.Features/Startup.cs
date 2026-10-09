using Microsoft.Extensions.DependencyInjection;
using Crest.Deployment;
using Crest.Features.Deployment;
using Crest.Features.Recipes.Executors;
using Crest.Features.Services;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Security.Permissions;

namespace Crest.Features;

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
        services.AddNavigationProvider<AdminMenu>();

        services.AddDeployment<AllFeaturesDeploymentSource, AllFeaturesDeploymentStep, AllFeaturesDeploymentStepDriver>();
    }
}
