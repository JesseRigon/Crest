using System.Text.Json.Nodes;
using Grove.DisplayManagement.Components;

namespace Grove.Tests.DisplayManagement;

// The tree renderer under HtmlRenderer (§ 3, § 8): one pass over the tree, zones as render
// fragments of the same pass, an unknown component rendering nothing, a markup leaf as its HTML.
public sealed class DisplayTreeRendererTests
{
    [Fact]
    public async Task RendersATreeAndItsZonesInOnePass()
    {
        var tree = new DisplayNode
        {
            Id = "root",
            Component = "Section",
            Zones = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Header"] = [Heading("h", "Title", 1)],
                ["ChildContent"] = [Heading("a", "First", 2), Heading("b", "Second", 3)],
            },
        };

        var html = await RenderAsync(tree);

        Assert.Equal("<section><header><h1>Title</h1></header><div><h2>First</h2><h3>Second</h3></div></section>", html);
    }

    [Fact]
    public async Task AnUnknownComponentRendersNothing()
    {
        var tree = new DisplayNode
        {
            Id = "root",
            Component = "Section",
            Zones = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ChildContent"] = [new DisplayNode { Id = "x", Component = "NotRegistered" }, Heading("a", "Kept", 2)],
            },
        };

        var html = await RenderAsync(tree);

        Assert.Equal("<section><header></header><div><h2>Kept</h2></div></section>", html);
    }

    [Fact]
    public async Task AnErrorNodeRendersNothing()
    {
        var html = await RenderAsync(DisplayNode.Error("e", new DisplayDiagnostic("e", "limit", "too many nodes")));

        Assert.Equal(string.Empty, html);
    }

    [Fact]
    public async Task AMarkupLeafRendersItsHtml()
    {
        var tree = new DisplayNode
        {
            Id = "root",
            Component = "Section",
            Zones = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ChildContent"] = [DisplayNode.Markup("m", "<p>Rich <em>text</em></p>")],
            },
        };

        var html = await RenderAsync(tree);

        Assert.Contains("<div><p>Rich <em>text</em></p></div>", html);
    }

    [Fact]
    public async Task AnUndeclaredZoneAndAnUnknownPropertyAreIgnored()
    {
        var heading = Heading("h", "Text", 4);
        heading.Properties["Unknown"] = "ignored";
        var tree = new DisplayNode
        {
            Id = "root",
            Component = "Section",
            Zones = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Sidebar"] = [Heading("s", "Nowhere", 2)],
                ["ChildContent"] = [heading],
            },
        };

        var html = await RenderAsync(tree);

        Assert.Equal("<section><header></header><div><h4>Text</h4></div></section>", html);
    }

    [Fact]
    public async Task AComponentBindingRendersTheShapeAndItsZonesInOnePass()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IComponentRegistry>(DisplayTreeTestHost.Registry());
        services.AddScoped<IDisplayTreeBuilder, DisplayTreeBuilder>();
        services.AddScoped<ComponentShapeRenderer>();
        services.AddOptions<DisplayTreeOptions>();
        await using var provider = services.BuildServiceProvider();

        var section = DisplayTreeTestHost.NewShape("Section");
        section.Properties["Header"] = await DisplayTreeTestHost.ZoneAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "Bound"), ("Level", 1)));
        var binding = global::Grove.DisplayManagement.Descriptors.ShapeBinding.ForComponent("Section", "Section");

        var html = (await binding.BindingAsync(new global::Grove.DisplayManagement.Implementation.DisplayContext
        {
            Value = section,
            ServiceProvider = provider,
        })).ToString();

        Assert.Equal("Section", binding.ComponentName);
        Assert.Equal("<section><header><h1>Bound</h1></header><div></div></section>", html);
    }

    private static DisplayNode Heading(string id, string text, int level) => new()
    {
        Id = id,
        Component = "Heading",
        Properties = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Text"] = text,
            ["Level"] = JsonValue.Create(level),
        },
    };

    private static Task<string> RenderAsync(DisplayNode node)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IComponentRegistry>(DisplayTreeTestHost.Registry());

        return ComponentShapeRenderer.RenderAsync(node, services.BuildServiceProvider(), NullLoggerFactory.Instance);
    }
}
