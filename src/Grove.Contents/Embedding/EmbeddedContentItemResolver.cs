using Grove.Contents.Management;
using YesSql;

namespace Grove.Contents.Embedding;

/// <summary>Resolves an embedded item's id through the embedded-item index (<see cref="EmbeddedContentItemIndex"/>) to its parent and embedding.</summary>
public sealed class EmbeddedContentItemResolver(ISession session, Grove.Contents.Management.Metadata.IContentDefinitionManager definitions) : IEmbeddedContentItemResolver
{
    public async Task<EmbeddedContentItemReference> ResolveAsync(string contentItemId, bool latest = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(contentItemId))
        {
            return null;
        }

        // Two queries rather than a conditional inside one: the SQL translation takes plain predicates.
        var parent = latest
            ? await session.Query<ContentItem, EmbeddedContentItemIndex>(index => index.ContentItemId == contentItemId && index.Latest).FirstOrDefaultAsync(cancellationToken)
            : await session.Query<ContentItem, EmbeddedContentItemIndex>(index => index.ContentItemId == contentItemId && index.Published).FirstOrDefaultAsync(cancellationToken);
        var embedding = parent is null
            ? null
            : (await session.QueryIndex<EmbeddedContentItemIndex>(index => index.ContentItemId == contentItemId && index.ParentContentItemId == parent.ContentItemId).FirstOrDefaultAsync(cancellationToken))?.EmbeddingName;
        if (embedding is null)
        {
            return null;
        }

        // A nested embedding holds the item at any depth.
        var declaration = (await definitions.GetTypeDefinitionAsync(parent.ContentType))?.Embeddings
            .FirstOrDefault(candidate => string.Equals(candidate.Name, embedding, StringComparison.Ordinal));
        var item = declaration is null
            ? parent.FindEmbedded(embedding, contentItemId)
            : declaration.AllItems(parent).FirstOrDefault(candidate => candidate.ContentItemId == contentItemId);
        return item is null ? null : new EmbeddedContentItemReference(parent, embedding, item);
    }
}
