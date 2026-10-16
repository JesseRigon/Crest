namespace Grove.Contents.Management;

/// <summary>
/// Resolves a reference to an embedded content item (docs/content-engine.md › Embedded content
/// items): an embedded item has no document of its own, so its id resolves to its parent, the
/// embedding holding it and the item.
/// </summary>
public interface IEmbeddedContentItemResolver
{
    /// <summary>The embedded item <paramref name="contentItemId"/> in its parent's published version (or latest, when <paramref name="latest"/>); null when none holds it.</summary>
    Task<EmbeddedContentItemReference> ResolveAsync(string contentItemId, bool latest = false, CancellationToken cancellationToken = default);
}

/// <summary>An embedded content item with the parent and embedding holding it.</summary>
public sealed record EmbeddedContentItemReference(ContentItem Parent, string Embedding, ContentItem Item);
