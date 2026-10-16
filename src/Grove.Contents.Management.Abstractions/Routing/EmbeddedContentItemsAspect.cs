using System.Text.Json.Nodes;

namespace Grove.Contents.Management.Routing;

/// <summary>
/// The embedded-items accessor (docs/content-engine.md › Embedded content items): where an item's
/// embedded content items are, so routing, taxonomies and the option list field's relationship
/// guard find them. Each accessor reads one embedding's items from an item's content.
/// </summary>
public class EmbeddedContentItemsAspect
{
    /// <summary>Json accessors, each answering one embedding's items (the relationship guard reads every embedding).</summary>
    public IList<Func<JsonObject, JsonArray>> Accessors { get; set; } = [];

    /// <summary>The accessors of the referenceable embeddings only: their items' ids are exposed to routing.</summary>
    public IList<Func<JsonObject, JsonArray>> ReferenceableAccessors { get; set; } = [];
}
