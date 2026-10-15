using Grove.Access;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace Grove.Server.Tests;

// The node filter of docs/blazor-display.md § 10 over a built tree (§ 4a: a menu item the viewer
// may not run is left out).
public sealed class DisplayTreeFilterTests
{
    private static readonly CallerContext s_viewer = CallerContext.Anonymous("Default", CallerSide.Site);

    [Fact]
    public async Task LeavesOutNodesWhoseWorkflowTheViewerMayNotRun()
    {
        var filter = Filter(new WorkflowFilter("allowed"));
        var root = Menu();

        var filtered = await filter.FilterAsync(root, s_viewer, TestContext.Current.CancellationToken);

        Assert.NotNull(filtered);
        var items = filtered.Zones["ChildContent"];
        Assert.Equal(["post"], items.Select(item => item.Id));
    }

    [Fact]
    public async Task UnlinksBindingsAndOverlayActionsToADroppedNode()
    {
        var filter = Filter(new WorkflowFilter("allowed"));
        var root = Menu();
        root.Zones["ChildContent"].Add(new DisplayNode
        {
            Id = "label",
            Component = "Item",
            Bindings = new() { ["Text"] = DisplayBinding.FromOutput("void", "Changed") },
            Events = new() { ["Clicked"] = [new DisplayAction { Kind = DisplayActionKind.Open, Target = "void" }, new DisplayAction { Kind = DisplayActionKind.Set, Target = "x" }] },
        });

        var filtered = await filter.FilterAsync(root, s_viewer, TestContext.Current.CancellationToken);

        var label = Assert.Single(filtered!.Zones["ChildContent"], node => node.Id == "label");
        Assert.Empty(label.Bindings!);
        Assert.Equal([DisplayActionKind.Set], label.Events!["Clicked"].Select(action => action.Kind));
    }

    [Fact]
    public async Task AWorkflowNobodyVouchesForIsNotShown()
    {
        var filter = Filter();

        var filtered = await filter.FilterAsync(Menu(), s_viewer, TestContext.Current.CancellationToken);

        Assert.Empty(filtered!.Zones["ChildContent"]);
    }

    [Fact]
    public async Task LeavesOutAComponentWhosePermissionTheViewerLacks()
    {
        var decision = Substitute.For<IAccessDecision>();
        decision.DecideAsync(Arg.Any<CallerContext>(), "ViewSecrets", Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(AccessDecision.Denied("no"));
        var filter = Filter(decision: decision);
        var root = new DisplayNode
        {
            Id = "root",
            Component = "Menu",
            Zones = new(StringComparer.OrdinalIgnoreCase) { ["ChildContent"] = [new DisplayNode { Id = "secret", Component = "Secret" }, new DisplayNode { Id = "item", Component = "Item" }] },
        };

        var filtered = await filter.FilterAsync(root, s_viewer, TestContext.Current.CancellationToken);

        Assert.Equal(["item"], filtered!.Zones["ChildContent"].Select(node => node.Id));
    }

    [Fact]
    public async Task NoCallerSeesNothingThatNeedsADecision()
    {
        var filtered = await Filter(new WorkflowFilter("allowed")).FilterAsync(Menu(), caller: null, TestContext.Current.CancellationToken);

        Assert.Empty(filtered!.Zones["ChildContent"]);
    }

    [Fact]
    public async Task SanitizesMarkupParametersBeforeTheTreeLeaves()
    {
        var sanitizer = Substitute.For<Grove.Infrastructure.Html.IHtmlSanitizerService>();
        sanitizer.Sanitize(Arg.Any<string>()).Returns(call => call.Arg<string>()!.Replace("<script>x</script>", string.Empty, StringComparison.Ordinal));
        var root = new DisplayNode
        {
            Id = "text",
            Component = "Text",
            Properties = new(StringComparer.OrdinalIgnoreCase) { ["Html"] = "<p>Hi</p><script>x</script>", ["Class"] = "<b>" },
        };

        var filtered = await Filter(sanitizer: sanitizer).FilterAsync(root, s_viewer, TestContext.Current.CancellationToken);

        Assert.Equal("<p>Hi</p>", filtered!.Properties["Html"]!.GetValue<string>());
        Assert.Equal("<b>", filtered.Properties["Class"]!.GetValue<string>());
    }

    private static DisplayNode Menu() => new()
    {
        Id = "root",
        Component = "Menu",
        Zones = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ChildContent"] =
            [
                new DisplayNode { Id = "post", Component = "Item", Events = new() { ["Clicked"] = [new DisplayAction { Kind = DisplayActionKind.Workflow, Workflow = "allowed" }] } },
                new DisplayNode { Id = "void", Component = "Item", Events = new() { ["Clicked"] = [new DisplayAction { Kind = DisplayActionKind.Workflow, Workflow = "refused" }] } },
            ],
        },
    };

    private static DisplayTreeFilter Filter(IDisplayActionFilter? actionFilter = null, IAccessDecision? decision = null, Grove.Infrastructure.Html.IHtmlSanitizerService? sanitizer = null)
    {
        var services = new ServiceCollection();
        if (sanitizer is not null)
        {
            services.AddSingleton(sanitizer);
        }

        if (actionFilter is not null)
        {
            services.AddSingleton(actionFilter);
        }

        services.AddSingleton(decision ?? Substitute.For<IAccessDecision>());
        var registry = new ComponentRegistry([new ListCatalog(
        [
            new ComponentDescriptor("Menu", typeof(ComponentBase), [], [new ZoneDescriptor("ChildContent", [])], ComponentSchema.AllShells),
            new ComponentDescriptor("Item", typeof(ComponentBase), [new ParameterDescriptor("Clicked", typeof(EventCallback), ParameterKind.EventHandler, "action")], [], ComponentSchema.AllShells),
            new ComponentDescriptor("Secret", typeof(ComponentBase), [], [], ComponentSchema.AllShells, Permission: "ViewSecrets"),
            new ComponentDescriptor("Text", typeof(ComponentBase),
            [
                new ParameterDescriptor("Html", typeof(string), ParameterKind.String, ComponentSchema.Editors.Markup),
                new ParameterDescriptor("Class", typeof(string), ParameterKind.String, ComponentSchema.Editors.Text),
            ], [], ComponentSchema.AllShells),
        ])]);

        return new DisplayTreeFilter(registry, services.BuildServiceProvider());
    }

    private sealed class WorkflowFilter(string allowed) : IDisplayActionFilter
    {
        public Task<bool?> AllowsAsync(CallerContext caller, DisplayAction action, CancellationToken cancellationToken = default) =>
            Task.FromResult<bool?>(action.Kind == DisplayActionKind.Workflow ? action.Workflow == allowed : null);
    }

    private sealed class ListCatalog(IEnumerable<ComponentDescriptor> components) : IComponentCatalog
    {
        public IEnumerable<ComponentDescriptor> Describe() => components;
    }
}
