using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Razor.Compilation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Components;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Descriptors.ShapeAttributeStrategy;
using Crest.DisplayManagement.Descriptors.ShapePlacementStrategy;
using Crest.DisplayManagement.Descriptors.ShapeTemplateStrategy;
using Crest.DisplayManagement.Events;
using Crest.DisplayManagement.Extensions;
using Crest.DisplayManagement.Implementation;
using Crest.DisplayManagement.Layout;
using Crest.DisplayManagement.LocationExpander;
using Crest.DisplayManagement.ModelBinding;
using Crest.DisplayManagement.Notify;
using Crest.DisplayManagement.Razor;
using Crest.DisplayManagement.Shapes;
using Crest.DisplayManagement.TagHelpers;
using Crest.DisplayManagement.Theming;
using Crest.DisplayManagement.Title;
using Crest.DisplayManagement.Zones;
using Crest.Environment.Extensions;
using Crest.Environment.Extensions.Features;
using Crest.Mvc.LocationExpander;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds host and tenant level services for managing themes.
    /// </summary>
    public static PlatformBuilder AddTheming(this PlatformBuilder builder)
    {
        builder.AddThemingHost()
            .ConfigureServices(services =>
            {
                services.Configure<MvcOptions>((options) =>
                {
                    options.Filters.Add<ModelBinderAccessorFilter>();
                    options.Filters.Add<NotifyFilter>();
                    options.Filters.Add<RazorViewActionFilter>();
                });

                services.AddTransient<IConfigureOptions<NotifyJsonSerializerOptions>, NotifyJsonSerializerOptionsConfiguration>();

                // Used as a service when we create a fake 'ActionContext'.
                services.AddScoped<IAsyncViewActionFilter, RazorViewActionFilter>();

                services.AddScoped<IUpdateModelAccessor, LocalModelBinderAccessor>();
                services.AddScoped<ViewContextAccessor>();

                services.AddScoped<RazorShapeTemplateViewEngine>();
                services.AddScoped<IShapeTemplateViewEngine>(sp => sp.GetService<RazorShapeTemplateViewEngine>());

                services.AddSingleton<IApplicationFeatureProvider<ViewsFeature>, ThemingViewsFeatureProvider>();
                services.AddScoped<IViewLocationExpanderProvider, ThemeViewLocationExpanderProvider>();

                services.AddScoped<IShapeTemplateHarvester, BasicShapeTemplateHarvester>();
                services.AddKeyedSingleton<IDictionary<string, Task<ShapeTable>>>(nameof(DefaultShapeTableManager), new ConcurrentDictionary<string, Task<ShapeTable>>());
                services.AddScoped<IShapeTableManager, DefaultShapeTableManager>();

                // The component registry (docs/blazor-display.md § 1): per tenant like the shape
                // table, rebuilt with the shell.
                services.AddSingleton<IComponentRegistryManager, ComponentRegistryManager>();
                services.AddOptions<ComponentRegistryOptions>();
                services.TryAddScoped<IShellKindAccessor, SiteShellKindAccessor>();
                services.AddScoped<IComponentRegistry>(sp => sp.GetRequiredService<IComponentRegistryManager>()
                    .GetRegistry(sp.GetRequiredService<IShellKindAccessor>().ShellKind));

                services.AddShapeTableProvider<ShapeAttributeBindingStrategy>();
                services.AddShapeTableProvider<ShapePlacementParsingStrategy>();
                services.AddShapeTableProvider<ShapeTemplateBindingStrategy>();

                services.AddScoped<IPlacementNodeFilterProvider, PathPlacementNodeFilterProvider>();

                services.AddScoped<IShapePlacementProvider, ShapeTablePlacementProvider>();

                services.AddTransient<IConfigureOptions<ShapeTemplateOptions>, ShapeTemplateOptionsSetup>();
                services.TryAddSingleton<IShapeTemplateFileProviderAccessor, ShapeTemplateFileProviderAccessor>();

                services.AddShapeAttributes<CoreShapes>();
                services.AddShapeTableProvider<CoreShapesTableProvider>();
                services.AddShapeAttributes<ZoneShapes>();
                services.AddShapeTableProvider<ZoneShapeAlternates>();
                services.AddShapeAttributes<GroupShapes>();

                services.AddScoped(typeof(IDisplayManager<>), typeof(DisplayManager<>));
                services.AddScoped<IHtmlDisplay, DefaultHtmlDisplay>();
                // The Blazor display beside IHtmlDisplay: shape tree -> display tree (IComponentRegistry
                // is registered with the component registry), and the HTML side of component bindings.
                services.AddScoped<IDisplayTreeBuilder, DisplayTreeBuilder>();
                services.AddScoped<IDisplayTreeFilter, DisplayTreeFilter>();
                services.AddScoped<IDisplayHtmlRenderer, DisplayHtmlRenderer>();
                services.AddScoped<ComponentShapeRenderer>();
                services.AddOptions<DisplayTreeOptions>();
                services.AddOptions<ShapeRenderingOptions>();
                services.AddScoped<ILayoutAccessor, LayoutAccessor>();
                services.AddScoped<IThemeManager, ThemeManager>();
                services.AddScoped<IPageTitleBuilder, PageTitleBuilder>();

                services.AddScoped<IShapeFactory, DefaultShapeFactory>();
                services.AddScoped<IDisplayHelper, DisplayHelper>();

                services.AddScoped<INotifier, Notifier>();

                services.AddShapeAttributes<DateTimeShapes>();
                services.AddShapeAttributes<PageTitleShapes>();

                services.AddTagHelpers<UserDisplayNameTagHelper>();
                services.AddTagHelpers<AddAlternateTagHelper>();
                services.AddTagHelpers<AddClassTagHelper>();
                services.AddTagHelpers<AddWrapperTagHelper>();
                services.AddTagHelpers<ClearAlternatesTagHelper>();
                services.AddTagHelpers<ClearClassesTagHelper>();
                services.AddTagHelpers<ClearWrappersTagHelper>();
                services.AddTagHelpers<DateTimeTagHelper>();
                services.AddTagHelpers<InputIsDisabledTagHelper>();
                services.AddTagHelpers<RemoveAlternateTagHelper>();
                services.AddTagHelpers<RemoveClassTagHelper>();
                services.AddTagHelpers<RemoveWrapperTagHelper>();
                services.AddTagHelpers<ShapeMetadataTagHelper>();
                services.AddTagHelpers<ShapeTagHelper>();
                services.AddTagHelpers<TimeSpanTagHelper>();
                services.AddTagHelpers<ValidationMessageTagHelper>();
                services.AddTagHelpers<ZoneTagHelper>();
            });

        return builder;
    }

    /// <summary>
    /// Adds host level services for managing themes.
    /// </summary>
    public static PlatformBuilder AddThemingHost(this PlatformBuilder builder)
    {
        var services = builder.ApplicationServices;

        services.AddTransient<IExtensionDependencyStrategy, ThemeExtensionDependencyStrategy>();
        services.AddTransient<IFeatureBuilderEvents, ThemeFeatureBuilderEvents>();
        services.AddTransient<IFeaturesProvider, ThemeFeaturesProvider>();

        return builder;
    }
}
