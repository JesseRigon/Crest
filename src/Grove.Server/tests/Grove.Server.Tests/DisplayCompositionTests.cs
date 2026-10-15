using System.Text.Json.Nodes;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Components;
using Grove.DisplayManagement.Descriptors;
using Grove.DisplayManagement.Implementation;
using Grove.DisplayManagement.Layout;
using Grove.DisplayManagement.Shapes;
using Grove.DisplayManagement.Theming;
using Grove.DisplayManagement.Zones;
using Grove.Layers.Models;
using Grove.Layers.Services;
using Grove.Rules;
using Grove.Rules.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Grove.Server.Tests;

// One tree, rendered once (rulings 2026-10-12): a template binds a shape ahead of its component
// and places the shape's own zones (§ 2, § 5), a layer rule's placements land in the layout's
// zones as shapes the builder maps (rework step 6).
public sealed class DisplayCompositionTests
{
    [Fact]
    public async Task ATemplateBindsAheadOfTheComponentAndPlacesTheShapesOwnZone()
    {
        var (services, shapes) = Services(new PlaceholderTemplate("Content"));
        var content = await shapes.CreateAsync("Content");
        await content.AddAsync(await Label(shapes, "From the driver"));

        var tree = await services.GetRequiredService<IDisplayTreeBuilder>().BuildAsync(content, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Card", tree.Root.Component);
        var label = Assert.Single(tree.Root.Zones["ChildContent"]);
        Assert.Equal("Label", label.Component);
        Assert.Equal("From the driver", label.Properties["Text"]!.GetValue<string>());
    }

    [Fact]
    public async Task WithoutATemplateTheComponentBinds()
    {
        var (services, shapes) = Services(new PlaceholderTemplate("Other"));
        var label = await Label(shapes, "Plain");

        var tree = await services.GetRequiredService<IDisplayTreeBuilder>().BuildAsync(label, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Label", tree.Root.Component);
    }

    [Fact]
    public async Task LayerPlacementsOfAdmittedLayersFillTheirZonesInOrder()
    {
        var (services, shapes) = Services();
        var layers = Substitute.For<ILayerService>();
        layers.GetLayersAsync().Returns(new LayersDocument
        {
            Layers = [new Layer { Name = "Always", LayerRule = new Rule() }, new Layer { Name = "Never", LayerRule = new Rule() }],
            Placements =
            [
                new LayerPlacement { Id = "second", Layer = "Always", Zone = "Footer", Component = "Label", Position = 2, Properties = new JsonObject { ["Text"] = "second" } },
                new LayerPlacement { Id = "first", Layer = "Always", Zone = "Footer", Component = "Label", Position = 1, Properties = new JsonObject { ["Text"] = "first" } },
                new LayerPlacement { Id = "hidden", Layer = "Never", Zone = "Footer", Component = "Label", Properties = new JsonObject { ["Text"] = "hidden" } },
                new LayerPlacement { Id = "unknown", Layer = "Always", Zone = "Footer", Component = "NotRegistered" },
            ],
        });

        var rules = Substitute.For<IRuleService>();
        var admitted = (await layers.GetLayersAsync()).Layers[0].LayerRule;
        rules.EvaluateAsync(Arg.Any<Rule>()).Returns(call => ValueTask.FromResult(ReferenceEquals(call.Arg<Rule>(), admitted)));

        var layout = new ZoneHolding(() => shapes.CreateAsync("Zone"));
        var contributor = new LayerTreeContributor(layers, rules, shapes, services.GetRequiredService<IComponentRegistry>(), NullLogger<LayerTreeContributor>.Instance);

        await contributor.ContributeAsync(new LayoutTreeContext(layout, null, null), TestContext.Current.CancellationToken);

        var footer = layout.Zones["Footer"];
        Assert.Equal(["first", "second"], footer.Items.Cast<IShape>().Select(shape => shape.Metadata.Name));
        Assert.Equal(["first", "second"], footer.Items.Cast<IShape>().Select(shape => shape.Properties["Text"] as string));
    }

    private static async Task<IShape> Label(IShapeFactory shapes, string text)
    {
        var shape = await shapes.CreateAsync("Label");
        shape.Properties["Text"] = text;
        return shape;
    }

    private static (ServiceProvider Services, IShapeFactory Shapes) Services(IDisplayTemplateBinding? binding = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTestDisplayTree(
            DisplayTreeTestServices.WithText("Label", typeof(ComponentBase)),
            new ComponentDescriptor("Card", typeof(ComponentBase), [], [new ZoneDescriptor("ChildContent", [])], ComponentSchema.AllShells),
            new ComponentDescriptor("Content", typeof(ComponentBase), [], [new ZoneDescriptor("ChildContent", [])], ComponentSchema.AllShells));
        if (binding is not null)
        {
            services.AddSingleton(binding);
        }

        var tables = Substitute.For<IShapeTableManager>();
        tables.GetShapeTableAsync(Arg.Any<string>()).Returns(new ShapeTable(new Dictionary<string, ShapeDescriptor>(), new Dictionary<string, ShapeBinding>()));
        var themes = Substitute.For<IThemeManager>();
        themes.GetThemeAsync().Returns(default(Grove.Environment.Extensions.IExtensionInfo));
        services.AddSingleton(tables);
        services.AddSingleton(themes);

        var provider = services.BuildServiceProvider();
        return (provider, new DefaultShapeFactory([], tables, themes, provider));
    }

    // A template for one shape type: a card holding the shape's own child items.
    private sealed class PlaceholderTemplate(string shapeType) : IDisplayTemplateBinding
    {
        public Task<DisplayNode?> BindAsync(DisplayTemplateRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(request.ShapeType != shapeType ? null : new DisplayNode
            {
                Id = request.InstanceId,
                Component = "Card",
                Zones = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["ChildContent"] =
                    [
                        new DisplayNode
                        {
                            Id = request.InstanceId + ".children",
                            Component = DisplayNode.ShapeZoneComponent,
                            Properties = new(StringComparer.OrdinalIgnoreCase) { [DisplayNode.ShapeZoneProperty] = "ChildContent" },
                        },
                    ],
                },
            });
    }
}
