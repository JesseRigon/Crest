using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Grove.Display.Management;
using Grove.Display.Management.Components;
using Grove.Display.Management.Descriptors;
using Grove.Display.Management.Descriptors.ShapePlacementStrategy;
using Grove.Display.Management.Events;
using Grove.Display.Management.Extensions;
using Grove.Display.Management.Implementation;
using Grove.Display.Management.Layout;
using Grove.Display.Management.Theming;
using Grove.Display.Management.Title;
using Grove.Display.Management.Zones;
using Grove.Environment.Extensions;
using Grove.Environment.Extensions.Features;
using Grove.Validation;

namespace Microsoft.Extensions.DependencyInjection;

public static class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds host and tenant level services for the display engine: themes, the shape table,
    /// placement, the display managers, the component registry and the display tree. The HTML
    /// shape renderers are added by <c>AddShapeRenderers</c> (Grove.Display.Management.Razor).
    /// </summary>
    public static PlatformBuilder AddTheming(this PlatformBuilder builder)
    {
        builder.AddThemingHost()
            .ConfigureServices(services =>
            {
                services.TryAddScoped<IValidationSink, ValidationSink>();

                services.AddKeyedSingleton<IDictionary<string, Task<ShapeTable>>>(nameof(DefaultShapeTableManager), new ConcurrentDictionary<string, Task<ShapeTable>>());
                services.AddScoped<IShapeTableManager, DefaultShapeTableManager>();

                // The component registry (docs/blazor-display.md § 1): per tenant like the shape
                // table, rebuilt with the shell.
                services.AddSingleton<IComponentRegistryManager, ComponentRegistryManager>();
                services.AddOptions<ComponentRegistryOptions>();
                services.TryAddScoped<IShellKindAccessor, SiteShellKindAccessor>();
                services.AddScoped<IComponentRegistry>(sp => sp.GetRequiredService<IComponentRegistryManager>()
                    .GetRegistry(sp.GetRequiredService<IShellKindAccessor>().ShellKind));

                services.AddShapeTableProvider<ShapePlacementParsingStrategy>();

                services.AddScoped<IPlacementNodeFilterProvider, PathPlacementNodeFilterProvider>();

                services.AddScoped<IShapePlacementProvider, ShapeTablePlacementProvider>();

                services.AddShapeTableProvider<ZoneShapeAlternates>();

                services.AddScoped(typeof(IDisplayManager<>), typeof(DisplayManager<>));
                // The display tree: shape tree -> display tree (IComponentRegistry is registered
                // with the component registry), its filter, versions, cache and HTML renderer.
                services.AddScoped<IDisplayTreeBuilder, DisplayTreeBuilder>();
                services.AddScoped<IDisplayTreeFilter, DisplayTreeFilter>();
                services.AddSingleton<DisplayTreeVersions>();
                services.AddSingleton<DisplayTreeCache>();
                services.AddSingleton<DisplayFallbackLog>();
                services.AddScoped<ComponentCoverage>();
                services.AddScoped<IDisplayHtmlRenderer, DisplayHtmlRenderer>();
                services.AddOptions<DisplayTreeOptions>();
                services.AddScoped<ILayoutAccessor, LayoutAccessor>();
                services.AddScoped<IThemeManager, ThemeManager>();
                services.AddScoped<IPageTitleBuilder, PageTitleBuilder>();

                services.AddScoped<IShapeFactory, DefaultShapeFactory>();
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
