using Grove.ContentManagement;
using Grove.Contents.Indexing;
using Grove.Indexing;
using Grove.Lists.Models;

namespace Grove.Lists.Indexes;

public class ContainedPartContentIndexHandler : IDocumentIndexHandler
{
    public Task BuildIndexAsync(BuildDocumentIndexContext context)
    {
        if (context.Record is not ContentItem contentItem)
        {
            return Task.CompletedTask;
        }

        if (!contentItem.TryGet<ContainedPart>(out var parent))
        {
            return Task.CompletedTask;
        }

        context.DocumentIndex.Set(
            ContentIndexingConstants.ContainedPartKey + ContentIndexingConstants.IdsKey,
            parent.ListContentItemId,
            DocumentIndexOptions.Keyword | DocumentIndexOptions.Store);

        context.DocumentIndex.Set(
            ContentIndexingConstants.ContainedPartKey + ContentIndexingConstants.OrderKey,
            parent.Order,
            DocumentIndexOptions.Keyword | DocumentIndexOptions.Store);

        return Task.CompletedTask;
    }
}
