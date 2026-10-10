using Fluid;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Handlers;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Deployment;
using Crest.DisplayManagement.Handlers;
using Crest.Layers.Deployment;
using Crest.Layers.Drivers;
using Crest.Layers.Handlers;
using Crest.Layers.Indexes;
using Crest.Layers.Models;
using Crest.Layers.Recipes;
using Crest.Layers.Services;
using Crest.Layers.ViewModels;
using Crest.Modules;
using Crest.Navigation;
using Crest.Recipes;
using Crest.Scripting;
using Crest.Security.Permissions;

namespace Crest.Layers;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<WidgetWrapper>();
        });

        services.Configure<MvcOptions>((options) =>
        {
            options.Filters.Add<LayerFilter>();
        });

        services.AddSiteDisplayDriver<LayerSiteSettingsDisplayDriver>();
        services.AddContentPart<LayerMetadata>();
        services.AddScoped<IContentDisplayDriver, LayerMetadataWelder>();
        services.AddNavigationProvider<AdminMenu>();
        services.AddScoped<ILayerService, LayerService>();
        services.AddScoped<IContentHandler, LayerMetadataHandler>();
        services.AddIndexProvider<LayerMetadataIndexProvider>();
        services.AddDataMigration<Migrations>();
        services.AddPermissionProvider<Permissions>();
        services.AddRecipeExecutionStep<LayerStep>();
        services.AddDeployment<AllLayersDeploymentSource, AllLayersDeploymentStep, AllLayersDeploymentStepDriver>();
        services.AddSingleton<IGlobalMethodProvider, DefaultLayersMethodProvider>();
    }
}
