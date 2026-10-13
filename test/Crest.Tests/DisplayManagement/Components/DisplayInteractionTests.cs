using System.Text.Json.Nodes;
using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

#nullable enable

namespace Crest.Tests.DisplayManagement.Components;

// docs/blazor-display.md § 4a: outputs, page state, bindings, event payloads, client actions.
public class DisplayInteractionTests
{
    [Fact]
    public void StateReadsOutputsVariablesEventsAndPaths()
    {
        var state = new DisplayState();
        state.SetOutput("invoices", "SelectedItem", new { id = "inv-7", lines = new[] { new { sku = "A" } } });
        state.SetVariable("row", JsonValue.Create("r1"));

        Assert.Equal("inv-7", state.Read(DisplayBinding.FromOutput("invoices", "SelectedItem", "id"))!.GetValue<string>());
        Assert.Equal("A", state.Read(DisplayBinding.FromOutput("invoices", "SelectedItem", "lines[0].SKU"))!.GetValue<string>());
        Assert.Equal("r1", state.Read(DisplayBinding.FromVariable("row"))!.GetValue<string>());
        Assert.Equal(5, state.Read(DisplayBinding.FromEvent("item.count"), new JsonObject { ["item"] = new JsonObject { ["count"] = 5 } })!.GetValue<int>());
        Assert.Equal("fallback", state.Read(new DisplayBinding { Node = "missing", Output = "X", Value = "fallback" })!.GetValue<string>());
        Assert.Equal(3, state.Read(DisplayBinding.Literal(3))!.GetValue<int>());
    }

    [Fact]
    public void StateOpensAndClosesOverlaysAndRaisesChanges()
    {
        var state = new DisplayState();
        var changes = 0;
        state.Changed += () => changes++;

        state.Open("menu", new DisplayPoint(10, 20), JsonValue.Create("row"));
        Assert.True(state.IsOpen("menu"));
        Assert.Equal(new DisplayPoint(10, 20), state.OpenState("menu").Position);

        state.Close("menu");
        state.Close("menu");
        Assert.False(state.IsOpen("menu"));
        Assert.Equal(2, changes);
    }

    [Fact]
    public async Task RunnerRunsClientAndServerActionsInOrder()
    {
        var state = new DisplayState();
        var dispatcher = new RecordingDispatcher { Result = new JsonObject { ["total"] = 12 } };
        var runner = new DisplayActionRunner(dispatcher, NullLogger.Instance);
        var payload = new JsonObject { ["item"] = new JsonObject { ["id"] = "ord-1" } };

        await runner.RunAsync(
            [
                new DisplayAction { Kind = DisplayActionKind.Set, Target = "row", Value = DisplayBinding.FromEvent("item") },
                new DisplayAction { Kind = DisplayActionKind.Open, Target = "rowMenu" },
                new DisplayAction
                {
                    Kind = DisplayActionKind.Workflow,
                    Workflow = "approve-order",
                    Target = "result",
                    Arguments = new() { ["order"] = DisplayBinding.FromVariable("row", "id") },
                },
                new DisplayAction { Kind = DisplayActionKind.Navigate, Value = DisplayBinding.Literal("invoices/inv-1") },
                new DisplayAction { Kind = DisplayActionKind.Close },
            ],
            sourceNodeId: "list",
            payload,
            new DisplayPoint(1, 2),
            overlay: "rowMenu",
            state,
            TestContext.Current.CancellationToken);

        Assert.Equal("ord-1", state.GetVariable("row")!["id"]!.GetValue<string>());
        Assert.Equal(("approve-order", "ord-1"), (dispatcher.Workflow, dispatcher.Input!["order"]!.GetValue<string>()));
        Assert.Equal(12, state.GetVariable("result")!["total"]!.GetValue<int>());
        Assert.Equal("invoices/inv-1", dispatcher.Navigated);
        Assert.False(state.IsOpen("rowMenu"));
    }

    [Fact]
    public async Task RunnerKeepsRefusedArgumentsUnderTheSourceNodeUntilARunSucceeds()
    {
        var state = new DisplayState();
        var refusing = new RefusingDispatcher();
        var actions = new List<DisplayAction> { new() { Kind = DisplayActionKind.Workflow, Workflow = "contact" } };

        await new DisplayActionRunner(refusing, NullLogger.Instance).RunAsync(actions, "form", null, null, null, state, TestContext.Current.CancellationToken);

        Assert.Equal(["The input 'email' is required."], state.GetErrors("form")["email"]);
        Assert.Empty(state.GetErrors("other"));

        await new DisplayActionRunner(new RecordingDispatcher(), NullLogger.Instance).RunAsync(actions, "form", null, null, null, state, TestContext.Current.CancellationToken);

        Assert.Empty(state.GetErrors("form"));
    }

    private sealed class RefusingDispatcher : IDisplayActionDispatcher
    {
        public Task<JsonNode?> RunWorkflowAsync(string workflow, IReadOnlyDictionary<string, JsonNode?> input, CancellationToken cancellationToken = default) =>
            throw new DisplayActionException("Invalid", new Dictionary<string, IReadOnlyList<string>> { ["email"] = ["The input 'email' is required."] });

        public void Navigate(string url)
        {
        }

        public Task SubmitAsync(string nodeId, JsonNode? values, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact]
    public async Task RunnerStopsTheListWhenAServerActionHasNoDispatcher()
    {
        var state = new DisplayState();
        var runner = new DisplayActionRunner(dispatcher: null, NullLogger.Instance);

        await runner.RunAsync(
            [
                new DisplayAction { Kind = DisplayActionKind.Workflow, Workflow = "w" },
                new DisplayAction { Kind = DisplayActionKind.Set, Target = "after", Value = DisplayBinding.Literal(1) },
            ],
            "node",
            null,
            null,
            null,
            state,
            TestContext.Current.CancellationToken);

        Assert.Null(state.GetVariable("after"));
    }

    [Fact]
    public void ValidatorAcceptsSoundWiring()
    {
        var root = Tree(out var registry);

        Assert.Empty(DisplayTreeValidator.Validate(root, registry));
    }

    [Fact]
    public void ValidatorReportsBrokenWiring()
    {
        var root = Tree(out var registry);
        var details = root.Zones["ChildContent"][1];
        details.Bindings!["Text"] = DisplayBinding.FromOutput("list", "Missing");
        details.Bindings["Count"] = DisplayBinding.FromOutput("list", "Selected");
        details.Bindings["Nope"] = DisplayBinding.FromVariable("x");
        root.Zones["ChildContent"][0].Events!["Selected"].Add(new DisplayAction { Kind = DisplayActionKind.Open, Target = "details" });
        root.Zones["ChildContent"][0].Events!["Text"] = [new DisplayAction { Kind = DisplayActionKind.Submit }];

        var reasons = DisplayTreeValidator.Validate(root, registry).Select(diagnostic => diagnostic.Reason).ToArray();

        Assert.Contains(reasons, reason => reason.Contains("'Missing' is not an output", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains("produces String, which 'Count' (Int32) does not take", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains("'Nope' is not a value parameter", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains("is not an overlay component", StringComparison.Ordinal));
        Assert.Contains(reasons, reason => reason.Contains("'Text' is not an event or output", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidatorRefusesBindingCyclesAndDuplicateIds()
    {
        var root = Tree(out var registry);
        var list = root.Zones["ChildContent"][0];
        list.Bindings = new() { ["Text"] = DisplayBinding.FromOutput("details", "Changed") };
        root.Zones["ChildContent"].Add(new DisplayNode { Id = "list", Component = "Label" });

        var actions = DisplayTreeValidator.Validate(root, registry).Select(diagnostic => diagnostic.Action).ToArray();

        Assert.Contains("cycle", actions);
        Assert.Contains("duplicate", actions);
    }

    [Fact]
    public async Task RendererReadsBoundValuesFromThePageState()
    {
        var root = Tree(out var registry);
        var state = new DisplayState();
        state.SetOutput("list", "Selected", "Invoice 7");

        var html = await RenderAsync(root, registry, state);

        Assert.Contains("<span class=\"label\">Invoice 7</span>", html);
    }

    [Fact]
    public async Task RendererShowsAnOverlayOnlyWhileItIsOpen()
    {
        var root = Tree(out var registry);
        root.Zones["ChildContent"].Add(new DisplayNode
        {
            Id = "menu",
            Component = "Overlay",
            Zones = new(StringComparer.OrdinalIgnoreCase) { ["ChildContent"] = [new DisplayNode { Id = "item", Component = "Label", Properties = new(StringComparer.OrdinalIgnoreCase) { ["Text"] = "Post" } }] },
        });

        var closed = await RenderAsync(root, registry, new DisplayState());
        var openState = new DisplayState();
        openState.Open("menu", new DisplayPoint(4, 5));
        var open = await RenderAsync(root, registry, openState);

        Assert.DoesNotContain("overlay", closed);
        Assert.Contains("<div class=\"overlay\" data-x=\"4\"><span class=\"label\">Post</span></div>", open);
    }

    [Fact]
    public async Task AnOutputFeedsTheNodesBoundToIt()
    {
        var registry = Registry();
        var root = new DisplayNode
        {
            Id = "root",
            Component = "Fragment",
            Zones = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ChildContent"] =
                [
                    new DisplayNode { Id = "emitter", Component = "Emitter", Properties = new(StringComparer.OrdinalIgnoreCase) { ["Text"] = "emit" } },
                    new DisplayNode { Id = "label", Component = "Label", Bindings = new() { ["Text"] = DisplayBinding.FromOutput("emitter", "Selected", "name") } },
                ],
            },
        };

        var state = new DisplayState();
        var html = await RenderAsync(root, registry, state);

        Assert.Equal("Emitted", state.GetOutput("emitter", "Selected")!["name"]!.GetValue<string>());
        Assert.Contains("<span class=\"label\">Emitted</span>", html);
    }

    [Fact]
    public async Task ABoundSlotReadsThroughTheDataSourceAndKeepsItsServerValueMeanwhile()
    {
        var registry = Registry();
        var source = new RecordingDataSource();
        var state = new DisplayState();
        state.SetVariable("customer", JsonValue.Create("c-1"));
        var root = new DisplayNode
        {
            Id = "label",
            Component = "Label",
            Properties = new(StringComparer.OrdinalIgnoreCase) { ["Text"] = "server value" },
            Slots = new() { ["Text"] = new DisplaySlot { Query = "Customer", Parameters = new() { ["id"] = DisplayBinding.FromVariable("customer") }, Path = "items[0].name" } },
        };

        var html = await RenderAsync(root, registry, state, source);

        Assert.Equal(("Customer", "c-1"), (source.Query, source.Parameters!["id"]!.GetValue<string>()));
        Assert.Contains("Acme", html);
    }

    // A list whose selection feeds a details label, and the components they use.
    private static DisplayNode Tree(out IComponentRegistry registry)
    {
        registry = Registry();
        return new DisplayNode
        {
            Id = "root",
            Component = "Fragment",
            Zones = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ChildContent"] =
                [
                    new DisplayNode
                    {
                        Id = "list",
                        Component = "Emitter",
                        Events = new() { ["Selected"] = [new DisplayAction { Kind = DisplayActionKind.Set, Target = "row", Value = DisplayBinding.FromEvent() }] },
                    },
                    new DisplayNode
                    {
                        Id = "details",
                        Component = "Label",
                        Bindings = new() { ["Text"] = DisplayBinding.FromOutput("list", "Selected") },
                    },
                ],
            },
        };
    }

    private static IComponentRegistry Registry() => new ComponentRegistry([new ListCatalog(
    [
        new ComponentDescriptor("Fragment", typeof(FragmentComponent), [], [new ZoneDescriptor("ChildContent", [])], ComponentSchema.AllShells),
        new ComponentDescriptor(
            "Label",
            typeof(LabelComponent),
            [
                new ParameterDescriptor("Text", typeof(string), ParameterKind.String, "text"),
                new ParameterDescriptor("Count", typeof(int), ParameterKind.Number, "number"),
                new ParameterDescriptor("Changed", typeof(EventCallback<string>), ParameterKind.Output, "output", ValueType: typeof(string)),
            ],
            [],
            ComponentSchema.AllShells),
        new ComponentDescriptor(
            "Emitter",
            typeof(EmitterComponent),
            [
                new ParameterDescriptor("Selected", typeof(EventCallback<JsonNode?>), ParameterKind.Output, "output", ValueType: typeof(string)),
                new ParameterDescriptor("Text", typeof(string), ParameterKind.String, "text"),
            ],
            [],
            ComponentSchema.AllShells),
        new ComponentDescriptor("Overlay", typeof(OverlayComponent), [], [new ZoneDescriptor("ChildContent", [])], ComponentSchema.AllShells, IsOverlay: true),
    ])]);

    private static async Task<string> RenderAsync(DisplayNode root, IComponentRegistry registry, DisplayState state, IDisplayDataSource? source = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(registry);
        if (source is not null)
        {
            services.AddSingleton(source);
        }

        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<DisplayTreeRenderer>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(DisplayTreeRenderer.Node)] = root,
                [nameof(DisplayTreeRenderer.State)] = state,
            }));

            // A slot's load or an output's change lands asynchronously; render again after it.
            for (var i = 0; i < 20 && source is RecordingDataSource { Completed: false }; i++)
            {
                await Task.Delay(10);
            }

            await output.QuiescenceTask;
            return output.ToHtmlString();
        });
    }

    private sealed class FragmentComponent : ComponentBase
    {
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
    }

    private sealed class LabelComponent : ComponentBase
    {
        [Parameter]
        public string? Text { get; set; }

        [Parameter]
        public int Count { get; set; }

        [Parameter]
        public EventCallback<string> Changed { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "class", "label");
            builder.AddContent(2, Text);
            builder.CloseElement();
        }
    }

    // Publishes its output once when its text says so, as a list would on a selection.
    private sealed class EmitterComponent : ComponentBase
    {
        [Parameter]
        public EventCallback<JsonNode?> Selected { get; set; }

        [Parameter]
        public string? Text { get; set; }

        protected override async Task OnInitializedAsync()
        {
            if (Text == "emit")
            {
                await Selected.InvokeAsync(new JsonObject { ["name"] = "Emitted" });
            }
        }
    }

    private sealed class OverlayComponent : ComponentBase
    {
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        [CascadingParameter]
        public DisplayOverlay? Overlay { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "class", "overlay");
            builder.AddAttribute(2, "data-x", Overlay?.Position?.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.AddContent(3, ChildContent);
            builder.CloseElement();
        }
    }

    private sealed class RecordingDispatcher : IDisplayActionDispatcher
    {
        public string? Workflow { get; private set; }

        public IReadOnlyDictionary<string, JsonNode?>? Input { get; private set; }

        public string? Navigated { get; private set; }

        public JsonNode? Result { get; init; }

        public Task<JsonNode?> RunWorkflowAsync(string workflow, IReadOnlyDictionary<string, JsonNode?> input, CancellationToken cancellationToken = default)
        {
            (Workflow, Input) = (workflow, input);
            return Task.FromResult(Result?.DeepClone());
        }

        public void Navigate(string url) => Navigated = url;

        public Task SubmitAsync(string nodeId, JsonNode? values, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingDataSource : IDisplayDataSource
    {
        public string? Query { get; private set; }

        public IReadOnlyDictionary<string, JsonNode?>? Parameters { get; private set; }

        public bool Completed { get; private set; }

        public Task<JsonNode?> QueryAsync(string query, IReadOnlyDictionary<string, JsonNode?> parameters, CancellationToken cancellationToken = default)
        {
            (Query, Parameters, Completed) = (query, parameters, true);
            return Task.FromResult<JsonNode?>(new JsonObject { ["items"] = new JsonArray(new JsonObject { ["name"] = "Acme" }) });
        }
    }

    private sealed class ListCatalog(IEnumerable<ComponentDescriptor> components) : IComponentCatalog
    {
        public IEnumerable<ComponentDescriptor> Describe() => components;
    }
}
