using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Liquid;
using Grove.Contents.Management.Display.Placement;
using Grove.Contents.Management.Display.ViewModels;
using Grove.Display.Management.Descriptors.ShapePlacementStrategy;
using Grove.Display.Liquid;

namespace Grove.Contents.Management.Display;

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
