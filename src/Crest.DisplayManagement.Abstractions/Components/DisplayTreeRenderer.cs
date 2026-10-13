using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Crest.DisplayManagement.Components;

/// <summary>
/// Renders a display tree (§ 3): for each node the registered component, its parameters
/// converted by the schema's CLR types, each zone as a <c>RenderFragment</c> over the zone's
/// nodes, keyed by the instance id. The same renderer runs under static SSR, in the browser and
/// under <c>HtmlRenderer</c>; nothing here touches <c>HttpContext</c> or JS. A node whose component
/// is not registered renders nothing; a markup leaf renders its (already sanitized) HTML.
/// <para>
/// Interaction (§ 4a): the outermost renderer of a tree owns its <see cref="DisplayState"/> and
/// cascades it. Bound parameters read the state; event and output parameters get callbacks
/// that record outputs and run the node's actions; an overlay node renders only while open. A
/// state change re-renders the tree, so every bound parameter sees the new value.
/// </para>
/// </summary>
public sealed class DisplayTreeRenderer : ComponentBase, IDisposable
{
    // Shared with validation (ComponentParameters), so what validates renders.
    private static readonly JsonSerializerOptions s_json = ComponentParameters.JsonOptions;

    private DisplayState? _ownState;
    private DisplayActionRunner? _runner;
    private IDisplayDataSource? _dataSource;

    [Parameter, EditorRequired]
    public DisplayNode Node { get; set; } = null!;

    /// <summary>The page state of an enclosing tree; absent at a tree's root, which creates its own.</summary>
    [CascadingParameter]
    public DisplayState? CascadedState { get; set; }

    /// <summary>A state the page that owns the tree holds, to read the tree's outputs (an editor's content) itself.</summary>
    [Parameter]
    public DisplayState? State { get; set; }

    [Inject]
    private IComponentRegistry Registry { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    private DisplayState CurrentState => _ownState ?? CascadedState!;

    protected override void OnInitialized()
    {
        // The tree's root owns the state (the page's, or a new one) and re-renders on its changes;
        // a renderer nested inside a tree shares the enclosing one.
        if (State is not null || CascadedState is null)
        {
            _ownState = State ?? new DisplayState();
            _ownState.Changed += OnStateChanged;
        }

        // The shell's dispatcher reads slots over the API too (a gated request per read, in a circuit
        // as in the browser); the in-process source serves the server's build.
        var dispatcher = Services.GetService<IDisplayActionDispatcher>();
        _dataSource = dispatcher as IDisplayDataSource ?? Services.GetService<IDisplayDataSource>();
        _runner = new DisplayActionRunner(
            dispatcher,
            Services.GetService<ILoggerFactory>()?.CreateLogger<DisplayTreeRenderer>() ?? NullLogger<DisplayTreeRenderer>.Instance);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var context = new RenderContext(Registry, CurrentState, this, _runner!, _dataSource);
        if (_ownState is null)
        {
            RenderNode(builder, Node, context, overlay: null);
            return;
        }

        builder.OpenComponent<CascadingValue<DisplayState>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<DisplayState>.Value), _ownState);
        builder.AddComponentParameter(2, nameof(CascadingValue<DisplayState>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<DisplayState>.ChildContent), (RenderFragment)(child => RenderNode(child, Node, context, overlay: null)));
        builder.CloseComponent();
    }

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        if (_ownState is not null)
        {
            _ownState.Changed -= OnStateChanged;
        }
    }

    /// <summary>Renders <paramref name="node"/> without interaction: no state, no callbacks (a static fragment).</summary>
    public static void RenderNode(RenderTreeBuilder builder, DisplayNode node, IComponentRegistry registry) =>
        RenderNode(builder, node, new RenderContext(registry, new DisplayState(), null, null, null), overlay: null);

    private sealed record RenderContext(IComponentRegistry Registry, DisplayState State, IHandleEvent? Receiver, DisplayActionRunner? Runner, IDisplayDataSource? DataSource);

    private static void RenderNode(RenderTreeBuilder builder, DisplayNode node, RenderContext context, string? overlay)
    {
        foreach (var (output, source) in node.OutputAliases ?? [])
        {
            context.State.RegisterAlias(node.Id, output, source);
        }

        builder.OpenRegion(0);
        if (string.Equals(node.Component, DisplayNode.MarkupComponent, StringComparison.OrdinalIgnoreCase))
        {
            var html = node.Properties.TryGetValue(DisplayNode.MarkupProperty, out var value) ? value?.GetValue<string>() : null;
            builder.AddMarkupContent(1, html ?? string.Empty);
        }
        else if (context.Registry.TryGet(node.Component, out var descriptor) && descriptor.Type is { } type)
        {
            if (descriptor.IsOverlay)
            {
                RenderOverlay(builder, node, descriptor, type, context);
            }
            else
            {
                RenderComponent(builder, node, descriptor, type, context, overlay);
            }
        }

        builder.CloseRegion();
    }

    // An overlay renders only while open, inside a cascaded DisplayOverlay that tells it where it
    // opened, what opened it and how it closes.
    private static void RenderOverlay(RenderTreeBuilder builder, DisplayNode node, ComponentDescriptor descriptor, Type type, RenderContext context)
    {
        if (!context.State.IsOpen(node.Id))
        {
            return;
        }

        var (position, payload) = context.State.OpenState(node.Id);
        var overlay = new DisplayOverlay
        {
            NodeId = node.Id,
            Position = position,
            Context = payload,
            CloseAsync = () =>
            {
                context.State.Close(node.Id);
                return Task.CompletedTask;
            },
        };

        builder.OpenComponent<CascadingValue<DisplayOverlay>>(10);
        builder.AddComponentParameter(11, nameof(CascadingValue<DisplayOverlay>.Value), overlay);
        builder.AddComponentParameter(12, nameof(CascadingValue<DisplayOverlay>.ChildContent), (RenderFragment)(child => RenderComponent(child, node, descriptor, type, context, node.Id)));
        builder.CloseComponent();
    }

    private static void RenderComponent(RenderTreeBuilder builder, DisplayNode node, ComponentDescriptor descriptor, Type type, RenderContext context, string? overlay)
    {
        builder.OpenComponent(2, type);
        builder.SetKey(node.Id);

        // Literal properties, then bound values over them once the bound value is there.
        var values = new Dictionary<string, JsonNode?>(node.Properties, StringComparer.OrdinalIgnoreCase);
        if (node.Bindings is { } bindings)
        {
            foreach (var (name, binding) in bindings)
            {
                if (context.State.Read(binding) is { } bound)
                {
                    values[name] = bound;
                }
            }
        }

        // A bound slot re-runs its query when its parameters change; until the result is
        // there the value the server resolved (if any) stays.
        if (node.Slots is { } slots && context.Receiver is not null)
        {
            foreach (var (name, slot) in slots)
            {
                if (!slot.IsStatic && context.State.TryGetSlot(slot, context.DataSource, out var result))
                {
                    values[name] = result;
                }
            }
        }

        foreach (var (name, raw) in values)
        {
            if (descriptor.Parameter(name) is { IsEvent: false } parameter && Convert(raw, parameter.ClrType) is { } converted)
            {
                builder.AddComponentParameter(3, parameter.Name, converted);
            }
        }

        if (context.Receiver is not null && context.Runner is not null)
        {
            foreach (var parameter in descriptor.Parameters)
            {
                if (!parameter.IsEvent)
                {
                    continue;
                }

                var actions = node.Events is { } events && events.TryGetValue(parameter.Name, out var list) ? list : null;
                if (parameter.Kind != ParameterKind.Output && actions is not { Count: > 0 })
                {
                    continue;
                }

                if (Callback(parameter, node, actions, context, overlay) is { } callback)
                {
                    builder.AddComponentParameter(4, parameter.Name, callback);
                }
            }
        }

        foreach (var (zoneName, children) in node.Zones)
        {
            if (descriptor.Zone(zoneName) is { } zone)
            {
                var nodes = children;
                builder.AddComponentParameter(5, zone.Name, (RenderFragment)(child =>
                {
                    foreach (var item in nodes)
                    {
                        RenderNode(child, item, context, overlay);
                    }
                }));
            }
        }

        builder.CloseComponent();
    }

    // An EventCallback or EventCallback<T> of the parameter's own type over one untyped handler:
    // record an output, then run the node's actions with the payload.
    [UnconditionalSuppressMessage("Trimming", "IL2067", Justification = "EventCallback<T> of a registered component's parameter type; its constructor is public and kept with the type.")]
    private static object? Callback(ParameterDescriptor parameter, DisplayNode node, List<DisplayAction>? actions, RenderContext context, string? overlay)
    {
        var state = context.State;
        var runner = context.Runner!;
        Func<object?, Task> handler = async argument =>
        {
            if (parameter.Kind == ParameterKind.Output)
            {
                state.SetOutput(node.Id, parameter.Name, argument);
            }

            if (actions is { Count: > 0 })
            {
                var payload = argument is null ? null : DisplayState.ToJson(argument);
                // A mouse event's payload carries its client position.
                var position = (argument as IDisplayPositioned)?.Position
                    ?? (payload is JsonObject pointer && pointer["clientX"] is JsonValue x && pointer["clientY"] is JsonValue y
                        && x.TryGetValue<double>(out var left) && y.TryGetValue<double>(out var top) ? new DisplayPoint(left, top) : null);
                await runner.RunAsync(actions, node.Id, payload, position, overlay, state);
            }
        };

        try
        {
            return parameter.ClrType == typeof(EventCallback)
                ? new EventCallback(context.Receiver, handler)
                : Activator.CreateInstance(parameter.ClrType, context.Receiver, handler);
        }
        catch (Exception exception) when (exception is ArgumentException or MissingMethodException or NotSupportedException)
        {
            return null;
        }
    }

    private static object? Convert(JsonNode? value, Type type)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            return value.Deserialize(type, s_json);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            return null;
        }
    }
}
