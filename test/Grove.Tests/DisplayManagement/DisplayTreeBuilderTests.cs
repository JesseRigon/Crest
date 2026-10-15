#nullable enable

using System.Text.Json.Nodes;
using Grove.Access;
using Grove.DisplayManagement;
using Grove.DisplayManagement.Components;
using Grove.DisplayManagement.Implementation;
using Grove.DisplayManagement.Shapes;
using Grove.Infrastructure.Html;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Tests.DisplayManagement;

// The shape-to-tree mapper (docs/blazor-display.md › Rework plan, step 2): binding through the
// registry, schema-checked properties, zones, deterministic ids, permissions, the markup
// fallback and the build limits.
public sealed class DisplayTreeBuilderTests
{
    [Fact]
    public async Task BindsAShapeToTheComponentRegisteredByItsType()
    {
        var builder = DisplayTreeTestHost.Builder();

        var tree = await builder.BuildAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "Hello")));

        Assert.Equal("Heading", tree.Root.Component);
        Assert.Equal("Tests", tree.Root.Module);
        Assert.Equal("Hello", tree.Root.Properties["Text"]!.GetValue<string>());
    }

    [Fact]
    public async Task ResolvesAlternatesLastToFirstThenTheTypeSegments()
    {
        var builder = DisplayTreeTestHost.Builder();

        var article = DisplayTreeTestHost.NewShape("Section");
        article.Metadata.Alternates.Add("Section__Article");
        article.Metadata.Alternates.Add("Section__Article__Unregistered");
        var segmented = DisplayTreeTestHost.NewShape("Heading__Big__Bold");

        Assert.Equal("Section__Article", (await builder.BuildAsync(article)).Root.Component);
        Assert.Equal("Heading", (await builder.BuildAsync(segmented)).Root.Component);
    }

    [Fact]
    public async Task DropsAndDiagnosesUnknownAndWrongTypedProperties()
    {
        var builder = DisplayTreeTestHost.Builder();
        var shape = DisplayTreeTestHost.NewShape("Heading", ("Text", "Title"), ("Level", "not a number"), ("Colour", "red"));

        var root = (await builder.BuildAsync(shape)).Root;

        Assert.True(root.Properties.ContainsKey("Text"));
        Assert.False(root.Properties.ContainsKey("Level"));
        Assert.False(root.Properties.ContainsKey("Colour"));
        Assert.Contains(root.Diagnostics!, d => d.Action == "dropped" && d.Path.EndsWith(".Level", StringComparison.Ordinal));
        Assert.Contains(root.Diagnostics!, d => d.Action == "dropped" && d.Reason.Contains("Colour", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConvertsCompatibleValuesThroughTheSchemaType()
    {
        var builder = DisplayTreeTestHost.Builder();

        var root = (await builder.BuildAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "T"), ("Level", 3)))).Root;

        Assert.Equal(3, root.Properties["Level"]!.GetValue<int>());
        Assert.Null(root.Diagnostics);
    }

    [Fact]
    public async Task MapsNamedZonesAndItemsAndDropsUndeclaredZones()
    {
        var builder = DisplayTreeTestHost.Builder();
        var section = DisplayTreeTestHost.NewShape("Section");
        section.Properties["Header"] = await DisplayTreeTestHost.ZoneAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "In the header")));
        section.Properties["Sidebar"] = await DisplayTreeTestHost.ZoneAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "Nowhere")));
        await section.AddAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "First")), "1");
        await section.AddAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "Second")), "2");

        var root = (await builder.BuildAsync(section)).Root;

        Assert.Equal("In the header", Assert.Single(root.Zones["Header"]).Properties["Text"]!.GetValue<string>());
        Assert.Equal(new[] { "First", "Second" }, root.Zones[DisplayTreeBuilder.ItemsZone].Select(n => n.Properties["Text"]!.GetValue<string>()));
        Assert.False(root.Zones.ContainsKey("Sidebar"));
        Assert.Contains(root.Diagnostics!, d => d.Action == "dropped" && d.Path.EndsWith("#Sidebar", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefusesAComponentAZoneDoesNotAdmit()
    {
        var builder = DisplayTreeTestHost.Builder();
        var list = DisplayTreeTestHost.NewShape("HeadingList");
        await list.AddAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "Kept")), "1");
        await list.AddAsync(DisplayTreeTestHost.NewShape("Section"), "2");

        var root = (await builder.BuildAsync(list)).Root;

        Assert.Equal("Heading", Assert.Single(root.Zones[DisplayTreeBuilder.ItemsZone]).Component);
        Assert.Contains(root.Diagnostics!, d => d.Reason.Contains("does not admit 'Section'", StringComparison.Ordinal));
    }

    [Fact]
    public async Task InstanceIdsAreDeterministicAndIndependentOfDroppedSiblings()
    {
        static async Task<IShape> BuildAsync(string itemId)
        {
            var section = DisplayTreeTestHost.NewShape("Section");
            section.Properties["ContentItem"] = new TestContentItem(itemId);
            await section.AddAsync(DisplayTreeTestHost.NewShape("Secret"), "1");

            var heading = DisplayTreeTestHost.NewShape("Heading", ("Text", "Same"));
            heading.Metadata.Name = "TitlePart";
            heading.Metadata.Differentiator = "TitlePart-Title";
            await section.AddAsync(heading, "2");

            return section;
        }

        var allowed = DisplayTreeTestHost.Builder(decision: DisplayTreeTestHost.Allow());
        var denied = DisplayTreeTestHost.Builder(decision: DisplayTreeTestHost.Deny());

        var first = (await allowed.BuildAsync(await BuildAsync("item-1"))).Root;
        var again = (await allowed.BuildAsync(await BuildAsync("item-1"))).Root;
        var other = (await allowed.BuildAsync(await BuildAsync("item-2"))).Root;
        var withoutSecret = (await denied.BuildAsync(await BuildAsync("item-1"))).Root;

        var heading = first.Zones[DisplayTreeBuilder.ItemsZone][1];
        Assert.Equal(first.Id, again.Id);
        Assert.Equal(heading.Id, again.Zones[DisplayTreeBuilder.ItemsZone][1].Id);
        Assert.NotEqual(first.Id, other.Id);
        Assert.NotEqual(heading.Id, other.Zones[DisplayTreeBuilder.ItemsZone][1].Id);

        // The viewer who may not see "Secret" gets the heading under the same id.
        Assert.Equal(heading.Id, Assert.Single(withoutSecret.Zones[DisplayTreeBuilder.ItemsZone]).Id);
    }

    [Fact]
    public async Task DropsANodeWhoseComponentPermissionTheViewerLacks()
    {
        var section = DisplayTreeTestHost.NewShape("Section");
        await section.AddAsync(DisplayTreeTestHost.NewShape("Secret"), "1");
        await section.AddAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "Public")), "2");

        var denied = (await DisplayTreeTestHost.Builder(decision: DisplayTreeTestHost.Deny()).BuildAsync(section)).Root;

        Assert.Equal("Heading", Assert.Single(denied.Zones[DisplayTreeBuilder.ItemsZone]).Component);
        Assert.Contains(denied.Diagnostics!, d => d.Action == "dropped" && d.Reason.Contains("ViewSecrets", StringComparison.Ordinal));

        var allowed = (await DisplayTreeTestHost.Builder(decision: DisplayTreeTestHost.Allow()).BuildAsync(section)).Root;

        Assert.Equal(new[] { "Secret", "Heading" }, allowed.Zones[DisplayTreeBuilder.ItemsZone].Select(n => n.Component));
    }

    [Fact]
    public async Task DropsAPermissionedNodeWhenThereIsNoCaller()
    {
        var builder = DisplayTreeTestHost.Builder(decision: DisplayTreeTestHost.Allow(), noCaller: true);

        var tree = await builder.BuildAsync(DisplayTreeTestHost.NewShape("Secret"));

        Assert.Equal(DisplayNode.MarkupComponent, tree.Root.Component);
        Assert.Null(tree.ScopeSignature);
    }

    [Fact]
    public async Task FallsBackToTheHtmlDisplayAsASanitizedMarkupLeaf()
    {
        var htmlDisplay = new Mock<IHtmlDisplay>();
        htmlDisplay.Setup(display => display.ExecuteAsync(It.IsAny<DisplayContext>()))
            .ReturnsAsync(new HtmlString("<p>Legacy</p><script>alert(1)</script>"));
        var builder = DisplayTreeTestHost.Builder(htmlDisplay: htmlDisplay.Object);

        var root = (await builder.BuildAsync(DisplayTreeTestHost.NewShape("Legacy__Widget"))).Root;

        Assert.Equal(DisplayNode.MarkupComponent, root.Component);
        var html = root.Properties[DisplayNode.MarkupProperty]!.GetValue<string>();
        Assert.Contains("<p>Legacy</p>", html);
        Assert.DoesNotContain("<script", html);
        Assert.Contains(root.Diagnostics!, d => d.Action == "markup-fallback");
    }

    [Fact]
    public async Task ABoundComponentTheRegistryDoesNotKnowIsAnErrorNotAFallback()
    {
        var htmlDisplay = new Mock<IHtmlDisplay>(MockBehavior.Strict);
        var builder = DisplayTreeTestHost.Builder(htmlDisplay: htmlDisplay.Object);

        var root = (await builder.BuildAsync(DisplayTreeTestHost.NewShape("Anything"), component: "Unregistered")).Root;

        Assert.Equal(DisplayNode.ErrorComponent, root.Component);
        Assert.Equal("unbound", Assert.Single(root.Diagnostics!).Action);
    }

    [Fact]
    public async Task ReplacesTheSubtreeWithAnErrorNodeWhenTheNodeCapIsReached()
    {
        var builder = DisplayTreeTestHost.Builder(options: new DisplayTreeOptions { MaxNodes = 2 });
        var section = DisplayTreeTestHost.NewShape("Section");
        for (var i = 0; i < 4; i++)
        {
            await section.AddAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", $"#{i}")), i.ToString(CultureInfo.InvariantCulture));
        }

        var root = (await builder.BuildAsync(section)).Root;
        var items = root.Zones[DisplayTreeBuilder.ItemsZone];

        Assert.Equal(new[] { "Heading", DisplayNode.ErrorComponent }, items.Select(n => n.Component));
        Assert.Equal("limit", Assert.Single(items[1].Diagnostics!).Action);
        Assert.Contains(root.Diagnostics!, d => d.Action == "omitted");
    }

    [Fact]
    public async Task ReplacesTooDeepABranchWithAnErrorNode()
    {
        var builder = DisplayTreeTestHost.Builder(options: new DisplayTreeOptions { MaxDepth = 1 });
        var outer = DisplayTreeTestHost.NewShape("Section");
        var inner = DisplayTreeTestHost.NewShape("Section");
        await inner.AddAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "Too deep")), "1");
        await outer.AddAsync(inner, "1");

        var root = (await builder.BuildAsync(outer)).Root;
        var tooDeep = Assert.Single(Assert.Single(root.Zones[DisplayTreeBuilder.ItemsZone]).Zones[DisplayTreeBuilder.ItemsZone]);

        Assert.Equal(DisplayNode.ErrorComponent, tooDeep.Component);
    }

    [Fact]
    public async Task StopsWhenTheTimeLimitIsSpent()
    {
        var builder = DisplayTreeTestHost.Builder(options: new DisplayTreeOptions { Timeout = TimeSpan.Zero });

        var root = (await builder.BuildAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", "Late")))).Root;

        Assert.Equal(DisplayNode.ErrorComponent, root.Component);
        Assert.Contains(root.Diagnostics!, d => d.Action == "limit");
    }

    [Fact]
    public async Task StopsWhenTheSizeCapIsReached()
    {
        var builder = DisplayTreeTestHost.Builder(options: new DisplayTreeOptions { MaxSize = 10 });

        var root = (await builder.BuildAsync(DisplayTreeTestHost.NewShape("Heading", ("Text", new string('x', 100))))).Root;

        Assert.Equal(DisplayNode.ErrorComponent, root.Component);
    }

    [Fact]
    public async Task CarriesTheViewersScopeSignatureAndTheDisplayType()
    {
        var builder = DisplayTreeTestHost.Builder();
        var shape = DisplayTreeTestHost.NewShape("Heading", ("Text", "T"));
        shape.Metadata.DisplayType = "Summary";

        var tree = await builder.BuildAsync(shape);

        Assert.Equal("Summary", tree.DisplayType);
        Assert.Equal(DisplayTreeTestHost.Caller.ScopeSignature, tree.ScopeSignature);
    }
}

internal sealed record TestContentItem(string ContentItemId);

internal static class DisplayTreeTestHost
{
    public static readonly CallerContext Caller = new()
    {
        Tenant = "Default",
        Side = CallerSide.Admin,
        UserId = "u1",
        UserName = "viewer",
    };

    public static ComponentRegistry Registry() => new([new TestComponentCatalog()]);

    public static DisplayTreeBuilder Builder(
        IAccessDecision? decision = null,
        IHtmlDisplay? htmlDisplay = null,
        DisplayTreeOptions? options = null,
        CallerContext? caller = null,
        bool noCaller = false)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IComponentRegistry>(Registry());
        services.AddSingleton<IHtmlSanitizerService>(new HtmlSanitizerService(Options.Create(new HtmlSanitizerOptions())));
        services.AddSingleton<ICallerContextAccessor>(new TestCallerAccessor { Current = noCaller ? null : caller ?? Caller });
        services.AddSingleton(decision ?? Allow());
        if (htmlDisplay is not null)
        {
            services.AddSingleton(htmlDisplay);
        }

        var provider = services.BuildServiceProvider();

        return new DisplayTreeBuilder(
            provider.GetRequiredService<IComponentRegistry>(),
            provider,
            Options.Create(options ?? new DisplayTreeOptions()),
            NullLogger<DisplayTreeBuilder>.Instance);
    }

    public static IAccessDecision Allow() => Decide(AccessDecision.Allowed);

    public static IAccessDecision Deny() => Decide(AccessDecision.Denied("not granted"));

    private static IAccessDecision Decide(AccessDecision verdict)
    {
        var decision = new Mock<IAccessDecision>();
        decision.Setup(d => d.DecideAsync(It.IsAny<CallerContext>(), It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(verdict);
        return decision.Object;
    }

    public static Shape NewShape(string type, params (string Name, object? Value)[] properties)
    {
        var shape = new Shape();
        shape.Metadata.Type = type;
        foreach (var (name, value) in properties)
        {
            shape.Properties[name] = value;
        }

        return shape;
    }

    public static async Task<IShape> ZoneAsync(params IShape[] children)
    {
        var zone = NewShape("ContentZone");
        for (var i = 0; i < children.Length; i++)
        {
            await zone.AddAsync(children[i], i.ToString(CultureInfo.InvariantCulture));
        }

        return zone;
    }

    private sealed class TestCallerAccessor : ICallerContextAccessor
    {
        public CallerContext? Current { get; set; }
    }

    private sealed class TestComponentCatalog : IComponentCatalog
    {
        private static readonly IReadOnlySet<ShellKind> s_anyShell = new HashSet<ShellKind>();

        public IEnumerable<ComponentDescriptor> Describe() =>
        [
            new("Heading", typeof(TestHeading),
                [
                    new ParameterDescriptor("Text", typeof(string), ParameterKind.String, "text"),
                    new ParameterDescriptor("Level", typeof(int), ParameterKind.Number, "number"),
                ],
                [], s_anyShell, Module: "Tests"),
            new("Section", typeof(TestSection), [],
                [new ZoneDescriptor("Header", []), new ZoneDescriptor("ChildContent", [])],
                s_anyShell, Module: "Tests"),
            new("Section__Article", typeof(TestSection), [],
                [new ZoneDescriptor("Header", []), new ZoneDescriptor("ChildContent", [])],
                s_anyShell, Module: "Tests"),
            new("HeadingList", typeof(TestSection), [],
                [new ZoneDescriptor("ChildContent", ["Heading"])],
                s_anyShell, Module: "Tests"),
            new("Secret", typeof(TestHeading), [], [], s_anyShell, Module: "Tests", Permission: "ViewSecrets"),
        ];
    }
}

public sealed class TestHeading : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public int Level { get; set; } = 2;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, $"h{Level}");
        builder.AddContent(1, Text);
        builder.CloseElement();
    }
}

public sealed class TestSection : ComponentBase
{
    [Parameter]
    public RenderFragment? Header { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "section");
        builder.OpenElement(1, "header");
        builder.AddContent(2, Header);
        builder.CloseElement();
        builder.OpenElement(3, "div");
        builder.AddContent(4, ChildContent);
        builder.CloseElement();
        builder.CloseElement();
    }
}
