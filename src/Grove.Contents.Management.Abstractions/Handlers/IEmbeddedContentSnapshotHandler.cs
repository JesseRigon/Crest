using Grove.Contents.Management.Metadata.Models;

namespace Grove.Contents.Management.Handlers;

/// <summary>
/// Freezes what a snapshot embedding's item keeps beyond its stored values (docs/content-engine.md
/// › Live and snapshot embeddings) when the parent is published: the option list field writes the
/// referenced values the embedding names beside its ids.
/// </summary>
public interface IEmbeddedContentSnapshotHandler
{
    Task FreezeAsync(EmbeddedContentSnapshotContext context, CancellationToken cancellationToken = default);
}

/// <summary>One embedded content item being frozen: the parent, the embedding, the item and its type's definition.</summary>
public sealed class EmbeddedContentSnapshotContext(ContentItem parent, EmbeddedContentDefinition embedding, ContentItem contentItem, ContentTypeDefinition contentType)
{
    public ContentItem Parent { get; } = parent;

    public EmbeddedContentDefinition Embedding { get; } = embedding;

    /// <summary>The embedded item; a handler writes into its content.</summary>
    public ContentItem ContentItem { get; } = contentItem;

    public ContentTypeDefinition ContentType { get; } = contentType;
}
