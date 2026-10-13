using Crest.Deployment;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.Layout;
using Crest.Layers.Deployment;
using Crest.Layers.Recipes;
using Crest.Layers.Services;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Scripting;
using Crest.Security.Permissions;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Layers;

/// <summary>
/// Layers as a build step (docs/blazor-display.md rework step 6): rules place component
/// instances into zone keys while a page's layout is built, edited on the admin Layers page
/// through <c>api/crest/layers</c>. Widgets and the MVC result filter are retired.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<AdminMenu>();
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
