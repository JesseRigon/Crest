using System.Text.Json.Nodes;
using Crest.ContentManagement;

namespace Crest.Templates.Models;

/// <summary>
/// An item's own page body (docs/widgets.md, ruling 2026-10-13): its display tree, in the node
/// format templates produce (components, properties, zones, bindings), stored as JSON on the
/// item. Rendered through the tree builder like any part, so templates, layer placements and
/// item bodies are one system and the node filter, the validator and the overrides apply to it
/// unchanged. Attachable; replaces the old flow body.
/// </summary>
public sealed class DisplayTreePart : ContentPart
{
    public JsonObject? Tree { get; set; }
}
