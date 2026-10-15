using Grove.Components.Primitives;
using Grove.DisplayManagement.Components;
using Grove.DisplayManagement.Implementation;
using Grove.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace Grove.Server.Tests;

// Exercises the actual end-to-end path: the component registry (built from Grove.Components'
// generated catalog) resolves a real component by its [BlazorComponent] name, and the
// resolver renders it via the display tree and HtmlRenderer to real HTML - not just "no
// exception thrown". Asserting on rendered content (not just a clean run) is what caught the
// real ParametersFromShape bug this suite found during development (see the integration test
// in this same project for that history).
public sealed class BlazorComponentShapeBindingResolverTests
{
    private static ComponentRegistry ComponentsRegistry() =>
        new([ComponentCatalogs.ForAssembly(typeof(GroveHeading).Assembly)]);

    [Fact]
    public void RegistryResolvesRealMarkedComponent()
    {
        var registry = ComponentsRegistry();

        Assert.True(registry.TryGet("GroveHeading", out var descriptor));
        Assert.Equal(typeof(GroveHeading), descriptor.Type);
        Assert.Equal("Grove.Components", descriptor.Module);
        Assert.Equal("Typography", descriptor.Category);

        var size = Assert.IsType<ParameterDescriptor>(descriptor.Parameter("Size"));
        Assert.Equal(ParameterKind.Choice, size.Kind);
        Assert.Equal(["H1", "H2", "H3", "H4", "H5", "H6"], size.Choices);
    }

    [Fact]
    public void RegistryFallsThroughForUnknownName()
    {
        var registry = ComponentsRegistry();

        Assert.False(registry.TryGet("NotARegisteredComponent", out _));
    }

    [Fact]
    public async Task ResolverRendersRealHtmlForRegisteredShapeType()
    {
        var registry = ComponentsRegistry();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, UnsupportedJSRuntime>();
        services.AddTestDisplayTree([.. registry.Components]);
        var serviceProvider = services.BuildServiceProvider();

        var resolver = new BlazorComponentShapeBindingResolver(registry);

        var binding = await resolver.GetShapeBindingAsync("GroveHeading");

        Assert.NotNull(binding);

        // The display tree mapper reads component parameters by the component's schema from
        // IShape.Properties, not from this model's own Parameters CLR property. A
        // GroveBlazorComponentPartDisplayDriver-built shape copies Parameters into
        // Properties explicitly for this reason; do the same here.
        var shape = new Grove.DisplayManagement.Shapes.Shape();
        shape.Properties["Text"] = "Hello Grove";
        var displayContext = new DisplayContext { Value = shape, ServiceProvider = serviceProvider };

        var html = (await binding.BindingAsync(displayContext)).ToString();

        Assert.Contains("rz-heading", html);
        Assert.Contains("Hello Grove", html);
    }

    [Fact]
    public async Task ResolverRendersCounterFromStoredStringParameter()
    {
        // The dev recipe's item: a part stores { "StartValue": "5" }, the driver binds it by the
        // schema and the renderer draws it.
        var registry = ComponentsRegistry();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IJSRuntime, UnsupportedJSRuntime>();
        services.AddTestDisplayTree([.. registry.Components]);
        var serviceProvider = services.BuildServiceProvider();

        Assert.True(registry.TryGet("GroveCounter", out var descriptor));
        var shape = new Grove.DisplayManagement.Shapes.Shape();
        foreach (var (key, value) in ComponentParameters.Bind(descriptor, [KeyValuePair.Create("StartValue", (object?)"5")]))
        {
            shape.Properties[key] = value;
        }

        var binding = await new BlazorComponentShapeBindingResolver(registry).GetShapeBindingAsync("GroveCounter");
        Assert.NotNull(binding);
        var html = (await binding.BindingAsync(new DisplayContext { Value = shape, ServiceProvider = serviceProvider })).ToString();

        Assert.Contains("Count: 5", html);
    }

    [Fact]
    public async Task ResolverFallsThroughForUnregisteredShapeType()
    {
        var resolver = new BlazorComponentShapeBindingResolver(ComponentsRegistry());

        var binding = await resolver.GetShapeBindingAsync("Content-Page");

        Assert.Null(binding);
    }

    [Fact]
    public async Task ResolverFallsThroughForComponentOutsideTheShell()
    {
        // A component limited to the admin shell is not in the site shell's registry.
        var adminOnly = DisplayTreeTestServices.WithText("AdminCard", typeof(GroveQuote)) with { Shells = new HashSet<ShellKind> { ShellKind.Admin } };
        var resolver = new BlazorComponentShapeBindingResolver(new ComponentRegistry([new ListCatalog([adminOnly])], ShellKind.Site));

        Assert.Null(await resolver.GetShapeBindingAsync("AdminCard"));
    }

    private sealed class ListCatalog(IEnumerable<ComponentDescriptor> components) : IComponentCatalog
    {
        public IEnumerable<ComponentDescriptor> Describe() => components;
    }
}
