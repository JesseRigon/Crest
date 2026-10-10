using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.ContentManagement.Display.Liquid;
using Crest.ContentManagement.Display.Placement;
using Crest.ContentManagement.Display.ViewModels;
using Crest.DisplayManagement.Descriptors.ShapePlacementStrategy;
using Crest.Liquid;

namespace Crest.ContentManagement.Display;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddContentManagementDisplay(this IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ContentItemViewModel>();
            o.MemberAccessStrategy.Register<ContentPartViewModel>();
        });

        services.TryAddTransient<IContentItemDisplayManager, ContentItemDisplayManager>();

        services.AddScoped<IContentDisplayHandler, ContentItemDisplayCoordinator>();

        services.AddScoped<IPlacementNodeFilterProvider, ContentTypePlacementNodeFilterProvider>();
        services.AddScoped<IPlacementNodeFilterProvider, ContentPartPlacementNodeFilterProvider>();

        services.AddScoped<IContentPartDisplayDriverResolver, ContentPartDisplayDriverResolver>();
        services.AddScoped<IContentFieldDisplayDriverResolver, ContentFieldDisplayDriverResolver>();

        services.AddOptions<ContentDisplayOptions>();

        services.AddLiquidFilter<ConsoleLogFilter>("console_log");

        return services;
    }
}
