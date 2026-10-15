using Grove.Deployment;
using Grove.DisplayManagement.Components;
using Grove.DisplayManagement.Layout;
using Grove.Layers.Deployment;
using Grove.Layers.Recipes;
using Grove.Layers.Services;
using Grove.Modules;
using Grove.Navigation;
using Grove.Recipes;
using Grove.Scripting;
using Grove.Security.Permissions;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Layers;

/// <summary>
/// Layers as a build step (docs/blazor-display.md rework step 6): rules place component
/// instances into zone keys while a page's layout is built, edited on the admin Layers page
/// through <c>api/grove/layers</c>. Widgets and the MVC result filter are retired.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ILayerService, LayerService>();
        services.AddScoped<LayerRuleReader>();
        services.AddScoped<ILayoutTreeContributor, LayerTreeContributor>();
        services.AddScoped<IDisplayTreeVersionPart, LayersTreeVersion>();
        services.AddPermissionProvider<Permissions>();
        services.AddRecipeExecutionStep<LayerStep>();
        services.AddDeployment<AllLayersDeploymentSource, AllLayersDeploymentStep, AllLayersDeploymentStepDriver>();
        services.AddSingleton<IGlobalMethodProvider, DefaultLayersMethodProvider>();
    }
}
