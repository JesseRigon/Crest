using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Liquid;
using Grove.ContentManagement.Display.Placement;
using Grove.ContentManagement.Display.ViewModels;
using Grove.DisplayManagement.Descriptors.ShapePlacementStrategy;
using Grove.Liquid;

namespace Grove.ContentManagement.Display;

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
