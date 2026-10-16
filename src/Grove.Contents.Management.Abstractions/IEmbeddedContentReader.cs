using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management;

/// <summary>
/// Reads an embedding's items with the definition each reads through (docs/content-engine.md ›
/// Live and snapshot embeddings): a live item, or an item a snapshot has not frozen, its type's
/// current definition; a frozen item, the schema stored beside it. Every reader of embedded items
/// that needs their definition (the display, the editor, workflows, GraphQL) takes it from here,
/// never from the current definition by the item's type name.
/// </summary>
public interface IEmbeddedContentReader
{
    /// <summary>The items of <paramref name="embedding"/> on <paramref name="parent"/> (every depth of a nested embedding), each with its definition.</summary>
    Task<IReadOnlyList<EmbeddedContentEntry>> ReadAsync(ContentItem parent, string embedding);

    /// <summary>As <see cref="ReadAsync(ContentItem, string)"/>, the parent read through <paramref name="parentDefinition"/> (itself a frozen item's schema, at a deeper level).</summary>
    Task<IReadOnlyList<EmbeddedContentEntry>> ReadAsync(ContentItem parent, ContentTypeDefinition parentDefinition, string embedding);
}

/// <summary>An embedded item, the definition it reads through, and whether that is a frozen schema.</summary>
public sealed record EmbeddedContentEntry(ContentItem Item, ContentTypeDefinition Definition, bool Frozen);
