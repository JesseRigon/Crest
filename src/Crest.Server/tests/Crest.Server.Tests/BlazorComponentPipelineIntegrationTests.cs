using Crest.ViewModels;
using Crest.Services;
using Crest.Models;
using Crest.Components.Primitives;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using NSubstitute;
using Crest.DisplayManagement;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Implementation;
using Crest.DisplayManagement.Theming;
using Xunit;

namespace Crest.Server.Tests;

// Goes one level deeper than BlazorComponentShapeBindingResolverTests: instead of
// hand-building a shape and calling the resolver directly,
// this drives the real chain a live request would use -
// IShapeFactory.CreateAsync<TModel>(shapeType, initialize) (Crest's real dynamic-proxy/
// Shape machinery, the same DefaultShapeFactory a live tenant uses, and the same call
// DisplayDriverBase.Initialize<TModel> makes internally on CrestBlazorComponentPart's
// behalf) -> IHtmlDisplay.ExecuteAsync with BlazorComponentShapeBindingResolver
// registered as one of N IShapeBindingResolvers, discovered by GetShapeBindingAsync's real
// resolver-enumeration loop (not called directly) -> real HtmlRenderer output.
// IShapeTableManager/IThemeManager are faked to an empty ShapeTable, since resolvers are
// checked before the table is ever consulted (DefaultHtmlDisplay.GetShapeBindingAsync) -
// nothing else in the display pipeline is faked.
public sealed class BlazorComponentPipelineIntegrationTests
{
    [Fact]
    public async Task ShapeFactoryAndHtmlDisplayRenderRealComponentThroughTheResolver()
    {
        var registryServices = new ServiceCollection();
        registryServices.AddLogging();
        registryServices.AddSingleton<IJSRuntime, UnsupportedJSRuntime>();
        var registry = new Crest.DisplayManagement.Components.ComponentRegistry(
            [Crest.DisplayManagement.Components.ComponentCatalogs.ForAssembly(typeof(CrestQuote).Assembly)]);
        registryServices.AddTestDisplayTree([.. registry.Components]);
        var registryServiceProvider = registryServices.BuildServiceProvider();

        var resolver = new BlazorComponentShapeBindingResolver(registry);

        var shapeTableManager = Substitute.For<IShapeTableManager>();
        shapeTableManager.GetShapeTableAsync(Arg.Any<string>())
            .Returns(new ShapeTable(new Dictionary<string, ShapeDescriptor>(), new Dictionary<string, ShapeBinding>()));

        var themeManager = Substitute.For<IThemeManager>();
        themeManager.GetThemeAsync().Returns(default(Crest.Environment.Extensions.IExtensionInfo));

        var displayServiceProvider = registryServiceProvider;

        // Same IShapeFactory implementation, and the same IHtmlDisplay implementation, a
        // live Crest request actually uses - only the theme/shape-table dependencies are
        // faked, since a real Blazor-rendered shape never reaches them (resolvers win first).
        var shapeFactory = new DefaultShapeFactory([], shapeTableManager, themeManager, displayServiceProvider);
        var shapeRenderingOptions = NSubstitute.Substitute.For<Microsoft.Extensions.Options.IOptionsMonitor<Crest.DisplayManagement.ShapeRenderingOptions>>();
        shapeRenderingOptions.CurrentValue.Returns(new Crest.DisplayManagement.ShapeRenderingOptions());
        var htmlDisplay = new DefaultHtmlDisplay(
            [],
            [resolver],
            shapeTableManager,
            displayServiceProvider,
            new NullLogger<DefaultHtmlDisplay>(),
            shapeRenderingOptions,
            themeManager);

        // A real shape from the shape factory, named after the component, its parameter in
        // Properties: what a driver or Liquid's shape_new produces.
        var shape = await shapeFactory.CreateAsync("CrestQuote");
        shape.Properties["Text"] = "Real content pipeline.";

        var html = (await htmlDisplay.ExecuteAsync(new Crest.DisplayManagement.Implementation.DisplayContext
        {
            Value = shape,
            ServiceProvider = displayServiceProvider,
        })).ToString();

        Assert.Contains("rz-quote", html);
        Assert.Contains("Real content pipeline.", html);
    }
}
