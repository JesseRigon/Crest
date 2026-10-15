using Grove.Api;
using Grove.Deployment;
using Grove.Display.Management.Components;
using Grove.Display.Management.Layout;
using Grove.Display.Layers.Deployment;
using Grove.Display.Layers.Recipes;
using Grove.Display.Layers.Services;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Infrastructure.Recipes;
using Grove.Scripting;
using Grove.Security.Permissions;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Display.Layers;

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
        services.AddDeployment<AllLayersDeploymentSource, AllLayersDeploymentStep>();
        services.AddSingleton<IGlobalMethodProvider, DefaultLayersMethodProvider>();
        services.AddApiContributor<Api.LayersApi>();
    }
}
