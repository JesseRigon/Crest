#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Access;
using Crest.DisplayManagement.Descriptors;
using Crest.DisplayManagement.Implementation;
using Crest.DisplayManagement.ModelBinding;
using Crest.DisplayManagement.Shapes;
using Crest.DisplayManagement.Theming;
using Crest.Infrastructure.Html;
using Crest.Modules;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Html;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crest.DisplayManagement.Components;

/// <summary>
/// The shape-to-tree mapper (decision 10: the shape system stays the backend and is mapped once
/// to the tree). Walks an <see cref="IShape"/> tree as the display managers build it and emits
/// one <see cref="DisplayTree"/>:
/// <list type="bullet">
/// <item>each shape's component is resolved through <see cref="IComponentRegistry.TryResolve"/>,
/// in <see cref="DefaultHtmlDisplay"/>'s order (alternates last to first, then the type's
/// segments);</item>
/// <item>properties are checked against the component's schema and converted to JSON; unknown
/// and wrong-typed ones are dropped and diagnosed;</item>
/// <item>zones (a zone holder's named zones, a shape's own items) become the node's zones;</item>
/// <item>instance ids are deterministic: content item, part, field, shape type and position;</item>
/// <item>a component whose descriptor names a permission the viewer lacks is left out (§ 10);</item>
/// <item>a shape with no component falls back to the Razor/Liquid <see cref="IHtmlDisplay"/>
/// and becomes a sanitized markup leaf (the transition path, until rework step 7);</item>
/// <item>the build runs under <see cref="DisplayTreeOptions"/>' limits.</item>
/// </list>
/// A child is never rendered to text and fed into its parent: the tree is assembled here and
/// rendered once (ruling 2026-10-12). The one exception is the fallback leaf, which is the
/// transition path and says so in its diagnostic.
/// </summary>
public sealed class DisplayTreeBuilder : IDisplayTreeBuilder
{
    /// <summary>The zone a shape's own items (not a named zone) map to: Blazor's child content.</summary>
    public const string ItemsZone = "ChildContent";

    // One conversion path: the renderer and the schema binder use the same options (enums by name),
    // so what validates here renders there.
    private static readonly JsonSerializerOptions s_json = ComponentParameters.JsonOptions;

    // The members every shape has; never component parameters, whatever the model type is.
    private static readonly HashSet<string> s_reservedMembers = new(
        typeof(IShape).GetProperties().Select(property => property.Name)
            .Concat(typeof(Shape).GetProperties().Select(property => property.Name)),
        StringComparer.OrdinalIgnoreCase);

    private static readonly ConcurrentDictionary<(Type Type, string Name), PropertyInfo?> s_modelProperties = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> s_contentItemIds = new();

    private readonly IComponentRegistry _registry;
    private readonly IServiceProvider _services;
    private readonly DisplayTreeOptions _options;
    private readonly ILogger _logger;

    public DisplayTreeBuilder(
        IComponentRegistry registry,
        IServiceProvider services,
        IOptions<DisplayTreeOptions> options,
        ILogger<DisplayTreeBuilder> logger)
    {
        _registry = registry;
        _services = services;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<DisplayTree> BuildAsync<TModel>(TModel model, IUpdateModel updater, string displayType = "", string groupId = "", CancellationToken cancellationToken = default)
    {
        var displayManager = _services.GetRequiredService<IDisplayManager<TModel>>();
        var shape = await displayManager.BuildDisplayAsync(model, updater, displayType, groupId);

        return await BuildAsync(shape, component: null, cancellationToken);
    }

    public async Task<DisplayTree> BuildAsync(IShape shape, string? component = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shape);

        var caller = _services.GetService<ICallerContextAccessor>()?.Current;
        var session = new Session(caller, _options, cancellationToken);
        var diagnostics = new List<DisplayDiagnostic>();

        var root = await MapAsync(shape, parentId: string.Empty, slot: "root", depth: 0, contentItemId: null, component, session, diagnostics)
            ?? DisplayNode.Markup(MakeId(string.Empty, ReadContentItemId(shape), shape, "root"), string.Empty);

        if (diagnostics.Count > 0)
        {
            (root.Diagnostics ??= []).AddRange(diagnostics);
        }

        if (session.Skipped > 0)
        {
            (root.Diagnostics ??= []).Add(new(root.Id, "omitted", $"{session.Skipped} further node(s) were left out after a limit was reached."));
        }

        // A placement's instance overrides (§ 5a), over everything the tree was built with.
        foreach (var overrides in _services.GetServices<IDisplayTreeOverrides>())
        {
            await overrides.ApplyAsync(root, cancellationToken);
        }

        return new DisplayTree
        {
            Root = root,
            DisplayType = shape.Metadata?.DisplayType,
            ScopeSignature = caller?.ScopeSignature,
        };
    }

    // Returns null when the node is dropped (a permission, an empty shape); the reason is
    // recorded in the parent's diagnostics.
    private async Task<DisplayNode?> MapAsync(
        IShape shape,
        string parentId,
        string slot,
        int depth,
        string? contentItemId,
        string? component,
        Session session,
        List<DisplayDiagnostic> parentDiagnostics)
    {
        if (session.Tripped)
        {
            session.Skipped++;
            return null;
        }

        if (shape.IsNullOrEmpty())
        {
            return null;
        }

        contentItemId = ReadContentItemId(shape) ?? contentItemId;
        var id = MakeId(parentId, contentItemId, shape, slot);

        if (session.Overrun(id, depth) is { } overrun)
        {
            return DisplayNode.Error(id, overrun);
        }

        // Pre-rendered content (a string or IHtmlContent added to a zone) is markup already.
        if (shape is PositionWrapper wrapper)
        {
            return MarkupLeaf(id, Write(PositionWrapper.UnWrap(wrapper)), session);
        }

        if (shape is IHtmlContent htmlContent)
        {
            return MarkupLeaf(id, Write(htmlContent), session);
        }

        var metadata = shape.Metadata;
        var type = metadata.Type;

        ComponentDescriptor? descriptor;
        if (component is not null)
        {
            if (!_registry.TryGet(component, out descriptor))
            {
                // A shape bound to a component the registry does not know: falling back to the
                // HTML display here would bind it to the same component again.
                return DisplayNode.Error(id, new(id, "unbound", $"The component '{component}' bound to '{type}' is not registered."));
            }
        }
        else if (string.IsNullOrEmpty(type))
        {
            parentDiagnostics.Add(new(id, "dropped", "The shape has no type."));
            return null;
        }
        else
        {
            await RunDisplayingAsync(shape, session);

            // A tenant template binds before the module's component (§ 2).
            if (await BindTemplateAsync(shape, id, depth, contentItemId, session, parentDiagnostics) is { } templated)
            {
                return templated;
            }

            if (!_registry.TryResolve(type, metadata.Alternates.ToArray(), out descriptor))
            {
                return await FallbackAsync(shape, id, session);
            }
        }

        if (!await IsPermittedAsync(descriptor, session))
        {
            parentDiagnostics.Add(new(id, "dropped", $"The viewer lacks the permission '{descriptor.Permission}' the component '{descriptor.Name}' requires."));
            return null;
        }

        var node = new DisplayNode
        {
            Id = id,
            Component = descriptor.Name,
            Module = descriptor.Module,
        };

        var diagnostics = new List<DisplayDiagnostic>();

        MapProperties(shape, descriptor, node, diagnostics, session);

        if (session.Tripped)
        {
            return DisplayNode.Error(id, session.Trip!);
        }

        await MapZonesAsync(shape, descriptor, node, depth, contentItemId, diagnostics, session);

        var mapped = await WrapAsync(node, metadata, diagnostics, session);

        if (diagnostics.Count > 0)
        {
            (node.Diagnostics ??= []).AddRange(diagnostics);
        }

        return mapped;
    }

    private static void MapProperties(IShape shape, ComponentDescriptor descriptor, DisplayNode node, List<DisplayDiagnostic> diagnostics, Session session)
    {
        foreach (var parameter in descriptor.Parameters)
        {
            // Zones and callbacks are not values: zones come from the shape's zones, actions
            // are wired by the renderer's host, never carried in the tree.
            if (parameter.IsEvent || IsCallback(parameter.ClrType))
            {
                continue;
            }

            if (!TryReadValue(shape, parameter.Name, out var value) || value is IShape)
            {
                if (parameter.Required)
                {
                    diagnostics.Add(new($"{node.Id}.{parameter.Name}", "missing", $"The required parameter '{parameter.Name}' has no value."));
                }

                continue;
            }

            if (!TryConvert(value, parameter.ClrType, out var json, out var reason))
            {
                diagnostics.Add(new($"{node.Id}.{parameter.Name}", "dropped", $"'{parameter.Name}' is not a {parameter.ClrType.Name}: {reason}"));
                continue;
            }

            node.Properties[parameter.Name] = json;
            session.Add(json?.ToJsonString(s_json).Length ?? 0);
        }

        var unknown = shape.Properties
            .Where(property => property.Value is not IShape && descriptor.Parameter(property.Key) is null)
            .Select(property => property.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (unknown.Length > 0)
        {
            diagnostics.Add(new(node.Id, "dropped", $"Not in the schema of '{descriptor.Name}': {string.Join(", ", unknown)}."));
        }
    }

    private async Task MapZonesAsync(
        IShape shape,
        ComponentDescriptor descriptor,
        DisplayNode node,
        int depth,
        string? contentItemId,
        List<DisplayDiagnostic> diagnostics,
        Session session)
    {
        // A zone holder keeps its zones as shape-valued properties (Zones["Content"] is
        // Properties["Content"]); a zone shape's items are the zone's children. Any other
        // shape-valued property is a one-child zone of that name.
        var sources = new List<(string Name, IReadOnlyList<object> Children)>();
        foreach (var (name, value) in shape.Properties)
        {
            if (value is IShape zone && !zone.IsNullOrEmpty())
            {
                sources.Add((name, IsZoneShape(zone) ? zone.Items.Cast<object>().ToArray() : [zone]));
            }
        }

        if (shape.HasItems())
        {
            sources.Add((ItemsZone, shape.Items.Cast<object>().ToArray()));
        }

        foreach (var (name, children) in sources)
        {
            if (session.Tripped)
            {
                return;
            }

            if (descriptor.Zone(name) is not { } zone)
            {
                diagnostics.Add(new($"{node.Id}#{name}", "dropped", $"'{descriptor.Name}' declares no zone '{name}'; its {children.Count} item(s) are left out."));
                continue;
            }

            var nodes = new List<DisplayNode>(children.Count);
            for (var index = 0; index < children.Count; index++)
            {
                // The slot uses the source index, before any child is dropped, so a sibling's
                // permission never changes another node's id.
                var slot = $"{zone.Name}[{index}]";
                var child = children[index] switch
                {
                    IShape childShape => await MapAsync(childShape, node.Id, slot, depth + 1, contentItemId, component: null, session, diagnostics),
                    string text => MarkupLeaf(MakeId(node.Id, contentItemId, null, slot), HtmlEncoder.Default.Encode(text), session),
                    IHtmlContent html => MarkupLeaf(MakeId(node.Id, contentItemId, null, slot), Write(html), session),
                    _ => null,
                };

                if (child is null)
                {
                    continue;
                }

                // Markup leaves are the fallback during the transition and are admitted
                // anywhere; a registered component must be one the zone allows.
                if (!IsMarkup(child) && child.Component != DisplayNode.ErrorComponent && !zone.Admits(child.Component))
                {
                    diagnostics.Add(new(child.Id, "dropped", $"The zone '{zone.Name}' of '{descriptor.Name}' does not admit '{child.Component}'."));
                    continue;
                }

                if (zone.Max > 0 && nodes.Count >= zone.Max)
                {
                    diagnostics.Add(new(child.Id, "dropped", $"The zone '{zone.Name}' of '{descriptor.Name}' holds at most {zone.Max} item(s)."));
                    continue;
                }

                nodes.Add(child);
            }

            if (nodes.Count < zone.Min)
            {
                diagnostics.Add(new($"{node.Id}#{zone.Name}", "invalid", $"The zone '{zone.Name}' of '{descriptor.Name}' needs at least {zone.Min} item(s); it has {nodes.Count}."));
            }

            if (nodes.Count > 0)
            {
                node.Zones[zone.Name] = nodes;
            }
        }
    }

    // The template's tree, with each placeholder replaced by the bound shape's zone, mapped in
    // this build (one tree, rendered once). Every node the template adds counts toward the limits.
    private async Task<DisplayNode?> BindTemplateAsync(IShape shape, string id, int depth, string? contentItemId, Session session, List<DisplayDiagnostic> parentDiagnostics)
    {
        var bindings = _services.GetServices<IDisplayTemplateBinding>().ToArray();
        if (bindings.Length == 0)
        {
            return null;
        }

        var metadata = shape.Metadata;
        var request = new DisplayTemplateRequest(shape, id, metadata.Type, metadata.Alternates.ToArray(), metadata.DisplayType);
        DisplayNode? root = null;
        foreach (var binding in bindings)
        {
            try
            {
                root = await binding.BindAsync(request, session.CancellationToken);
            }
            catch (Exception ex) when (!ex.IsFatal())
            {
                _logger.LogWarning(ex, "The template bound to '{ShapeType}' failed.", metadata.Type);
                return DisplayNode.Error(id, new(id, "error", $"The template bound to '{metadata.Type}' failed: {ex.Message}"));
            }

            if (root is not null)
            {
                break;
            }
        }

        if (root is null)
        {
            return null;
        }

        await FillShapeZonesAsync(root, shape, depth, contentItemId, session, parentDiagnostics);
        return session.Tripped ? DisplayNode.Error(id, session.Trip!) : root;
    }

    private async Task FillShapeZonesAsync(DisplayNode node, IShape shape, int depth, string? contentItemId, Session session, List<DisplayDiagnostic> diagnostics)
    {
        if (session.Overrun(node.Id, depth) is { } overrun)
        {
            (node.Diagnostics ??= []).Add(overrun);
            node.Zones.Clear();
            return;
        }

        foreach (var (zoneName, children) in node.Zones.ToArray())
        {
            var filled = new List<DisplayNode>(children.Count);
            foreach (var child in children)
            {
                if (!string.Equals(child.Component, DisplayNode.ShapeZoneComponent, StringComparison.Ordinal))
                {
                    await FillShapeZonesAsync(child, shape, depth + 1, contentItemId, session, diagnostics);
                    filled.Add(child);
                    continue;
                }

                var name = child.Properties.TryGetValue(DisplayNode.ShapeZoneProperty, out var value) && value is JsonValue text && text.TryGetValue<string>(out var zone) ? zone : ItemsZone;
                var items = ShapeZoneItems(shape, name);
                for (var index = 0; index < items.Count; index++)
                {
                    var slot = $"{name}[{index}]";
                    var mapped = items[index] switch
                    {
                        IShape itemShape => await MapAsync(itemShape, child.Id, slot, depth + 1, contentItemId, component: null, session, diagnostics),
                        string markup => MarkupLeaf(MakeId(child.Id, contentItemId, null, slot), HtmlEncoder.Default.Encode(markup), session),
                        IHtmlContent html => MarkupLeaf(MakeId(child.Id, contentItemId, null, slot), Write(html), session),
                        _ => null,
                    };

                    if (mapped is not null)
                    {
                        filled.Add(mapped);
                    }
                }
            }

            node.Zones[zoneName] = filled;
        }
    }

    private static IReadOnlyList<object> ShapeZoneItems(IShape shape, string name)
    {
        if (string.Equals(name, ItemsZone, StringComparison.OrdinalIgnoreCase))
        {
            return shape.HasItems() ? shape.Items.Cast<object>().ToArray() : [];
        }

        foreach (var (key, value) in shape.Properties)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase) && value is IShape zone && !zone.IsNullOrEmpty())
            {
                return IsZoneShape(zone) ? zone.Items.Cast<object>().ToArray() : [zone];
            }
        }

        return [];
    }

    // A shape's wrappers are components with one zone that take the wrapped node as their child
    // (§ 3); a wrapper with no such component is left out and diagnosed.
    private async Task<DisplayNode> WrapAsync(DisplayNode node, ShapeMetadata metadata, List<DisplayDiagnostic> diagnostics, Session session)
    {
        var wrapped = node;
        foreach (var wrapperName in metadata.Wrappers)
        {
            if (!_registry.TryGet(wrapperName, out var wrapper) || !wrapper.IsWrapper)
            {
                diagnostics.Add(new(node.Id, "unwrapped", $"No wrapper component is registered as '{wrapperName}'."));
                continue;
            }

            if (!await IsPermittedAsync(wrapper, session))
            {
                diagnostics.Add(new(node.Id, "unwrapped", $"The viewer lacks the permission '{wrapper.Permission}' the wrapper '{wrapper.Name}' requires."));
                continue;
            }

            var zone = wrapper.Zones.Count > 0 ? wrapper.Zones[0].Name : ItemsZone;
            wrapped = new DisplayNode
            {
                Id = MakeId(wrapped.Id, null, null, "wrapper:" + wrapper.Name),
                Component = wrapper.Name,
                Module = wrapper.Module,
                Zones = new(StringComparer.OrdinalIgnoreCase) { [zone] = [wrapped] },
            };
        }

        return wrapped;
    }

    // The displaying phase DefaultHtmlDisplay runs before it binds: the shape descriptor's and the
    // shape's own Displaying callbacks add the alternates the binding resolves by (a part's
    // per-type alternates, a display type's). The global IShapeDisplayEvents are not run here:
    // they serve the HTML render (caching, debug output) and still run when a shape falls back to
    // it. Always true; it only orders the phase before the resolution.
    private async Task<bool> RunDisplayingAsync(IShape shape, Session session)
    {
        var context = new ShapeDisplayContext
        {
            Shape = shape,
            ServiceProvider = _services,
            DisplayContext = new DisplayContext
            {
                Value = shape,
                ServiceProvider = _services,
                DisplayHelper = _services.GetService<IDisplayHelper>(),
                HtmlFieldPrefix = shape.Metadata.Prefix ?? string.Empty,
            },
        };

        if (await session.ShapeTableAsync(_services) is { } shapeTable
            && DefaultHtmlDisplay.GetShapeDescriptor(shape.Metadata.Type, shapeTable) is { } shapeDescriptor)
        {
            await shapeDescriptor.DisplayingAsync.InvokeAsync((action, displayContext) => action(displayContext), context, _logger);
        }

        shape.Metadata.Displaying.Invoke((action, displayContext) => action(displayContext), context, _logger);

        // Then the processing phase, where shapes populate themselves (a menu reads its items
        // here), as DefaultHtmlDisplay runs it before it renders. A shape that falls back to the
        // HTML display is processed again there; the fallback is the transition path.
        if (await session.ShapeTableAsync(_services) is { } table
            && DefaultHtmlDisplay.GetShapeDescriptor(shape.Metadata.Type, table) is { } descriptor)
        {
            await descriptor.ProcessingAsync.InvokeAsync((action, displayContext) => action(displayContext), context, _logger);
        }

        await shape.Metadata.ProcessingAsync.InvokeAsync((action, displayContext) => action(displayContext.Shape), context, _logger);

        return true;
    }

    // TODO(blazor-display step 7): the transition path. A shape no component is registered for
    // still renders through the Razor/Liquid shape engines and enters the tree as a sanitized
    // markup leaf; it goes when IHtmlDisplay and the Razor and Liquid engines are retired.
    private async Task<DisplayNode> FallbackAsync(IShape shape, string id, Session session)
    {
        string html;
        try
        {
            var htmlDisplay = _services.GetRequiredService<IHtmlDisplay>();
            var content = await htmlDisplay.ExecuteAsync(new DisplayContext
            {
                Value = shape,
                ServiceProvider = _services,
                DisplayHelper = _services.GetService<IDisplayHelper>(),
                HtmlFieldPrefix = shape.Metadata.Prefix ?? string.Empty,
            });
            html = Write(content);
        }
        catch (Exception ex) when (!ex.IsFatal())
        {
            _logger.LogWarning(ex, "The fallback render of the shape '{ShapeType}' failed.", shape.Metadata.Type);
            return DisplayNode.Error(id, new(id, "error", $"The shape '{shape.Metadata.Type}' has no component and its Razor/Liquid render failed."));
        }

        var leaf = MarkupLeaf(id, html, session);
        if (leaf.Component == DisplayNode.MarkupComponent)
        {
            (leaf.Diagnostics ??= []).Add(new(id, "markup-fallback",
                $"No component is registered for '{shape.Metadata.Type}'; it was rendered by the Razor/Liquid shape engine (transition until rework step 7)."));
        }

        return leaf;
    }

    // Markup is sanitized on render (§ 10). Without the platform's sanitizer no markup leaves the server.
    private DisplayNode MarkupLeaf(string id, string html, Session session)
    {
        if (_services.GetService<IHtmlSanitizerService>() is not { } sanitizer)
        {
            return DisplayNode.Error(id, new(id, "dropped", "No HTML sanitizer is registered; markup is not emitted unsanitized."));
        }

        var sanitized = string.IsNullOrEmpty(html) ? string.Empty : sanitizer.Sanitize(html);
        session.Add(sanitized.Length);

        return session.Tripped ? DisplayNode.Error(id, session.Trip!) : DisplayNode.Markup(id, sanitized);
    }

    private async Task<bool> IsPermittedAsync(ComponentDescriptor descriptor, Session session)
    {
        if (string.IsNullOrEmpty(descriptor.Permission))
        {
            return true;
        }

        // No caller or no decision: a node that requires a permission is not shown.
        if (session.Caller is null || _services.GetService<IAccessDecision>() is not { } decision)
        {
            return false;
        }

        if (!session.Permissions.TryGetValue(descriptor.Permission, out var allowed))
        {
            allowed = (await decision.DecideAsync(session.Caller, descriptor.Permission, cancellationToken: session.CancellationToken)).IsAllowed;
            session.Permissions[descriptor.Permission] = allowed;
        }

        return allowed;
    }

    private static bool IsCallback(Type type) =>
        typeof(Delegate).IsAssignableFrom(type)
        || type == typeof(EventCallback)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>));

    private static bool IsMarkup(DisplayNode node) => string.Equals(node.Component, DisplayNode.MarkupComponent, StringComparison.OrdinalIgnoreCase);

    // A position wrapper is pre-rendered content, never a zone (and has no items to ask for).
    private static bool IsZoneShape(IShape shape) =>
        shape is not PositionWrapper
        && (shape.Metadata?.Type is "Zone" or "ContentZone" || (string.IsNullOrEmpty(shape.Metadata?.Type) && shape.HasItems()));

    private static bool TryReadValue(IShape shape, string name, out object? value)
    {
        foreach (var (key, candidate) in shape.Properties)
        {
            if (string.Equals(key, name, StringComparison.OrdinalIgnoreCase))
            {
                value = candidate;
                return true;
            }
        }

        // A typed shape (ShapeResult.Initialize<TModel>) is a proxy of the view model: its
        // values are the model's CLR properties, not dictionary entries.
        var property = s_modelProperties.GetOrAdd((shape.GetType(), name), static key =>
        {
            if (s_reservedMembers.Contains(key.Name))
            {
                return null;
            }

            var candidate = key.Type.GetProperty(key.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            return candidate is { CanRead: true } && candidate.GetIndexParameters().Length == 0 ? candidate : null;
        });

        if (property is not null)
        {
            value = property.GetValue(shape);
            return true;
        }

        value = null;
        return false;
    }

    internal static bool TryConvert(object? value, Type type, out JsonNode? json, out string reason)
    {
        json = null;
        reason = string.Empty;

        if (value is null)
        {
            if (type.IsValueType && Nullable.GetUnderlyingType(type) is null)
            {
                reason = "null is not a value of a non-nullable type";
                return false;
            }

            return true;
        }

        try
        {
            json = value is JsonNode node ? node.DeepClone() : JsonSerializer.SerializeToNode(value, value.GetType(), s_json);

            // The renderer converts with the same options, so what validates here renders there.
            _ = json is null ? null : json.Deserialize(type, s_json);
            return true;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException or FormatException)
        {
            json = null;
            reason = ex.Message;
            return false;
        }
    }

    private static string? ReadContentItemId(IShape shape)
    {
        if (shape.Properties is null || !shape.Properties.TryGetValue("ContentItem", out var item) || item is null)
        {
            return null;
        }

        // The display management does not reference content management: the item's id is read
        // by name, as Razor and Liquid templates read it.
        var property = s_contentItemIds.GetOrAdd(item.GetType(), static type => type.GetProperty("ContentItemId", BindingFlags.Public | BindingFlags.Instance));
        return property?.GetValue(item) as string;
    }

    /// <summary>The instance id: a hash of the parent's id, the content item, the part (the shape's
    /// name), the field (its differentiator), the shape type and the position. The same build gives
    /// the same ids, whoever views it.</summary>
    internal static string MakeId(string parentId, string? contentItemId, IShape? shape, string slot)
    {
        var metadata = shape?.Metadata;
        var key = string.Join('\u001f',
            parentId,
            contentItemId ?? string.Empty,
            metadata?.Name ?? string.Empty,
            metadata?.Differentiator ?? string.Empty,
            metadata?.Type ?? string.Empty,
            metadata?.Position ?? string.Empty,
            slot);

        return "n" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..16];
    }

    private static string Write(IHtmlContent? content)
    {
        if (content is null)
        {
            return string.Empty;
        }

        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private sealed class Session(CallerContext? caller, DisplayTreeOptions options, CancellationToken cancellationToken)
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private int _nodes;
        private long _size;

        public CallerContext? Caller { get; } = caller;

        public CancellationToken CancellationToken { get; } = cancellationToken;

        public Dictionary<string, bool> Permissions { get; } = new(StringComparer.OrdinalIgnoreCase);

        public DisplayDiagnostic? Trip { get; private set; }

        public bool Tripped => Trip is not null;

        public int Skipped { get; set; }

        private Task<ShapeTable?>? _shapeTable;

        /// <summary>The current theme's shape table, read once per build; null where there is none.</summary>
        public Task<ShapeTable?> ShapeTableAsync(IServiceProvider services) => _shapeTable ??= ReadShapeTableAsync(services);

        private static async Task<ShapeTable?> ReadShapeTableAsync(IServiceProvider services) =>
            services.GetService<IThemeManager>() is { } themes && services.GetService<IShapeTableManager>() is { } tables
                ? await themes.GetShapeTableAsync(tables)
                : null;

        /// <summary>Counts a node and checks the limits; a non-null result is the reason this
        /// node becomes an error node. Depth is local to the branch; the others stop the build.</summary>
        public DisplayDiagnostic? Overrun(string id, int depth)
        {
            if (depth > options.MaxDepth)
            {
                return new(id, "limit", $"The tree is deeper than {options.MaxDepth} levels here.");
            }

            if (++_nodes > options.MaxNodes)
            {
                return Trip = new(id, "limit", $"The tree has more than {options.MaxNodes} nodes.");
            }

            if (_clock.Elapsed > options.Timeout || CancellationToken.IsCancellationRequested)
            {
                return Trip = new(id, "limit", $"Building the tree took longer than {options.Timeout.TotalSeconds:0.###} s.");
            }

            return null;
        }

        public void Add(long size)
        {
            _size += size;
            if (_size > options.MaxSize && Trip is null)
            {
                Trip = new(string.Empty, "limit", $"The tree carries more than {options.MaxSize} characters.");
            }
        }
    }
}
