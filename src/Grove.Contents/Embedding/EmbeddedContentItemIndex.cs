using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Data;
using Microsoft.Extensions.DependencyInjection;
using YesSql.Indexes;

namespace Grove.Contents.Embedding;

/// <summary>
/// The embedded-item index (docs/content-engine.md › Embedded content items): one row per
/// embedded content item of a parent's embeddings (a nested embedding's every item, depth first),
/// keyed by the item's stable id, with its position as the order. A module keeps its own domain indexes over embedded items.
/// </summary>
public sealed class EmbeddedContentItemIndex : MapIndex
{
    /// <summary>The parent content item.</summary>
    public string ParentContentItemId { get; set; }

    public string EmbeddingName { get; set; }

    /// <summary>The embedded item's stable id.</summary>
    public string ContentItemId { get; set; }

    public string ContentType { get; set; }

    public int Position { get; set; }

    public bool Published { get; set; }

    public bool Latest { get; set; }
}

public sealed class EmbeddedContentItemIndexProvider(IServiceProvider services) : IndexProvider<ContentItem>, IScopedIndexProvider
{
    // Resolved when first indexing: the definition manager reaches the session.
    private IContentDefinitionManager _definitions;

    public override void Describe(DescribeContext<ContentItem> context)
    {
        context.For<EmbeddedContentItemIndex>()
            .Map(async contentItem =>
            {
                // Soft-deleted items (neither published nor latest) drop out, as in every content index.
                if (!contentItem.Published && !contentItem.Latest)
                {
                    return [];
                }

                _definitions ??= services.GetRequiredService<IContentDefinitionManager>();
                var type = await _definitions.GetTypeDefinitionAsync(contentItem.ContentType);
                if (type is null || !type.Embeddings.Any())
                {
                    return [];
                }

                return type.Embeddings
                    .SelectMany(embedding => embedding.AllItems(contentItem).Select((item, position) => new EmbeddedContentItemIndex
                    {
                        ParentContentItemId = contentItem.ContentItemId,
                        EmbeddingName = embedding.Name,
                        ContentItemId = item.ContentItemId,
                        ContentType = item.ContentType,
                        Position = position,
                        Published = contentItem.Published,
                        Latest = contentItem.Latest,
                    }))
                    .Where(row => !string.IsNullOrEmpty(row.ContentItemId))
                    .ToArray();
            });
    }
}
