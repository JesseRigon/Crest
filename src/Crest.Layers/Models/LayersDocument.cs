using System.Text.Json.Nodes;
using Crest.Data.Documents;

namespace Crest.Layers.Models;

/// <summary>
/// The layers and what they place (docs/blazor-display.md, rulings 2026-10-12: widgets are
/// retired, layer rules fill zone keys with component instances).
/// </summary>
public class LayersDocument : Document
{
    public List<Layer> Layers { get; set; } = [];

    public List<LayerPlacement> Placements { get; set; } = [];
}

/// <summary>
/// A component instance a layer places into a zone key on every page its rule admits (all blog
/// posts, signed-in users): the component's registry name, its parameter values, its order in
/// the zone. Built into the page's layout before the tree is mapped, so it is a node of the one
/// tree like everything else, permissioned by its component and by the node filter.
/// </summary>
public class LayerPlacement
{
    /// <summary>The instance id (§ 5a): stable across edits, what overrides would bind to.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The layer whose rule admits it.</summary>
    public string Layer { get; set; } = string.Empty;

    /// <summary>The zone key it fills (<c>Footer</c>, <c>Sidebar</c>, a module's own key).</summary>
    public string Zone { get; set; } = string.Empty;

    /// <summary>The component's registry name.</summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>The component's parameter values, checked against its schema when saved and when built.</summary>
    public JsonObject Properties { get; set; } = [];

    /// <summary>The order in the zone; lower first.</summary>
    public double Position { get; set; }

    public string? Title { get; set; }
}
