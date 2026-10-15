using JetBrains.Annotations;
using Grove.Workflows.Documents;
using YesSql.Indexes;

namespace Grove.Workflows.Indexes;

[UsedImplicitly]
public class BookmarkQueueItemIndexProvider : IndexProvider<BookmarkQueueItemDocument>
{
    public BookmarkQueueItemIndexProvider()
    {
        CollectionName = WorkflowCollections.BookmarkQueue;
    }

    public override void Describe(DescribeContext<BookmarkQueueItemDocument> context)
    {
        context.For<BookmarkQueueItemIndex>().Map(document => new()
        {
            ItemId = document.ItemId,
            WorkflowInstanceId = document.WorkflowInstanceId,
            BookmarkId = document.BookmarkId,
            StimulusHash = document.StimulusHash,
            ActivityInstanceId = document.ActivityInstanceId,
            ActivityTypeName = document.ActivityTypeName,
            CreatedAt = document.CreatedAt,
        });
    }
}
