using System.Text.Json.Nodes;

namespace Grove.Display.Management.Components;

/// <summary>
/// One node of the display tree (§ 3, decision 10): the wire format the server builds and the
/// browser, a headless client or the HTML renderer draws. A node names a registered component,
/// carries its resolved property values (never expressions: the tree is evaluated on the server)
/// and its zones' children. The whole tree is assembled and then rendered in one pass; a child is
/// never rendered to text and fed into its parent (ruling 2026-10-12).
/// </summary>
public sealed class DisplayNode
{
    /// <summary>The instance id: deterministic from item, part and field, or the template's own;
    /// the render key and what an override binds to (§ 5a).</summary>
    public required string Id { get; init; }

    /// <summary>The registered component's name (the resolved binding).</summary>
    public required string Component { get; init; }

    /// <summary>The module that owns the component, so a headless client knows whether it supports it (decision 2).</summary>
    public string? Module { get; init; }

    public Dictionary<string, JsonNode?> Properties { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, List<DisplayNode>> Zones { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parameters whose value is read at run time from another node's output or a page
    /// variable (§ 4a), by parameter name; a bound value replaces the property of the same name
    /// once it is there.</summary>
    public Dictionary<string, DisplayBinding>? Bindings { get; set; }

    /// <summary>Parameters fed by a query, by parameter name (decision 4); the server puts a static
    /// slot's value in <see cref="Properties"/>, the browser re-runs a bound one.</summary>
    public Dictionary<string, DisplaySlot>? Slots { get; set; }

    /// <summary>Outputs this node re-publishes from nodes inside it (a composite's declared outputs),
    /// by output name: a binding to <c>{this node}.{output}</c> reads the inner node's output.</summary>
    public Dictionary<string, DisplayBinding>? OutputAliases { get; set; }

    /// <summary>The actions run, in order, when an event or output parameter fires, by parameter name (§ 4a).</summary>
    public Dictionary<string, List<DisplayAction>>? Events { get; set; }

    /// <summary>Why a node was changed or dropped while the tree was built (a missing permission, an
    /// unknown component, an invalid property); for the designer and diagnostics, never rendered.</summary>
    public List<DisplayDiagnostic>? Diagnostics { get; set; }

    /// <summary>The component name of a markup leaf: sanitized HTML in <see cref="MarkupProperty"/>.</summary>
    public const string MarkupComponent = "Markup";

    public const string MarkupProperty = "html";

    public static DisplayNode Markup(string id, string sanitizedHtml) => new()
    {
        Id = id,
        Component = MarkupComponent,
        Properties = new(StringComparer.OrdinalIgnoreCase) { [MarkupProperty] = sanitizedHtml },
    };

    /// <summary>
    /// The component name of a template's placeholder for the bound shape's own zone (§ 5): the
    /// tree builder replaces it with that zone's children, mapped in the same build, so a
    /// template places the driver-built parts without rendering them. Never reaches a renderer.
    /// </summary>
    public const string ShapeZoneComponent = "ShapeZone";

    /// <summary>The placeholder's property naming the shape zone (<c>Content</c>, <c>Header</c>, <c>ChildContent</c>).</summary>
    public const string ShapeZoneProperty = "zone";

    /// <summary>The component that renders its children and nothing else: a template with several
    /// top-level nodes, a zone's contents moved as one.</summary>
    public const string FragmentComponent = "Fragment";

    /// <summary>
    /// A parameter the renderer fills with the node's instance id when the component declares it
    /// (a form that reads the errors its actions returned from <see cref="DisplayState"/>). Never a
    /// stored value: the renderer's id wins over anything a template sets.
    /// </summary>
    public const string InstanceIdParameter = "InstanceId";

    /// <summary>The component name of an error node: a subtree that could not be built (a render
    /// limit, a failed fallback). It renders nothing unless a theme registers a component by this
    /// name; the reason is in <see cref="Diagnostics"/> (ruling 2026-10-12: limits fall back, they
    /// do not retry).</summary>
    public const string ErrorComponent = "DisplayError";

    public static DisplayNode Error(string id, DisplayDiagnostic diagnostic) => new()
    {
        Id = id,
        Component = ErrorComponent,
        Diagnostics = [diagnostic],
    };
}

public sealed record DisplayDiagnostic(string Path, string Action, string Reason);

/// <summary>A built tree and what it was built for: the cache key's parts (§ Data model).</summary>
public sealed class DisplayTree
{
    public required DisplayNode Root { get; init; }

    public string? DisplayType { get; init; }

    /// <summary>The viewer's scope signature the tree was built with: a tree is never served to another scope.</summary>
    public string? ScopeSignature { get; init; }
}
