using System.Text.Json.Nodes;

namespace Grove.Contents.Management;

/// <summary>
/// One embedding's embedded content items in an item (docs/content-engine.md › Embedded content
/// items), stored as <c>{ "&lt;EmbeddingName&gt;": { "ContentItems": [ … ] } }</c>; a snapshot
/// embedding stores the schema its items were written in beside them (<see cref="Schema"/>).
/// Read with <see cref="EmbeddedContentItemsExtensions.Embedded"/>, written with
/// <see cref="EmbeddedContentItemsExtensions.AlterEmbedded"/>.
/// </summary>
public sealed class EmbeddedContentItems : ContentElement
{
    /// <summary>The embedded content items, in order. Each keeps its own id, type, values and item properties.</summary>
    public List<ContentItem> ContentItems { get; set; } = [];

    /// <summary>
    /// A snapshot embedding's frozen schema: by content type, the type's definition (its field
    /// sets, fields, field types and settings) as the items were written in it. Null for a live
    /// embedding, and for a snapshot embedding not yet published.
    /// </summary>
    public JsonObject Schema { get; set; }
}

public static class EmbeddedContentItemsExtensions
{
    /// <summary>The embedded content items of the embedding <paramref name="embedding"/>; empty when it holds none.</summary>
    public static IReadOnlyList<ContentItem> Embedded(this ContentItem contentItem, string embedding) =>
        contentItem.Get<EmbeddedContentItems>(embedding)?.ContentItems ?? [];

    /// <summary>Changes the embedding <paramref name="embedding"/>'s items and writes them back into the item.</summary>
    public static ContentItem AlterEmbedded(this ContentItem contentItem, string embedding, Action<EmbeddedContentItems> action)
    {
        contentItem.Alter(embedding, action);
        return contentItem;
    }

    /// <summary>The embedded content item <paramref name="contentItemId"/> in the embedding <paramref name="embedding"/>, or null.</summary>
    public static ContentItem FindEmbedded(this ContentItem contentItem, string embedding, string contentItemId) =>
        contentItem.Embedded(embedding).FirstOrDefault(item => string.Equals(item.ContentItemId, contentItemId, StringComparison.Ordinal));
}

/// <summary>
/// Embeddings whose display a module owns (a taxonomy's term tree, a menu's items), as
/// <c>{Type}.{Embedding}</c>: the generic <c>EmbeddedContentItems</c> display leaves them out.
/// </summary>
public sealed class EmbeddedContentDisplayOptions
{
    public HashSet<string> DisplayedElsewhere { get; } = new(StringComparer.OrdinalIgnoreCase);
}
