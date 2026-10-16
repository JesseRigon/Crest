using Grove.Contents.Management;
using Grove.Contents.Indexing;
using Grove.Data.Indexing;
using Grove.Contents.Hierarchy.Models;

namespace Grove.Contents.Hierarchy.Indexes;

public class ParentContentItemFieldSetContentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ContentItem contentItem)
        {
            return Task.CompletedTask;
        }

        if (!contentItem.TryGet<ParentContentItemFieldSet>(out var parent))
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(
            ContentIndexingConstants.ParentContentItemKey + ContentIndexingConstants.IdsKey,
            parent.ListContentItemId,
            DocumentIndexOptions.Keyword | DocumentIndexOptions.Store);

        context.DocumentIndex.Set(
            ContentIndexingConstants.ParentContentItemKey + ContentIndexingConstants.OrderKey,
            parent.Order,
            DocumentIndexOptions.Keyword | DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
