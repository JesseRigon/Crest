using JetBrains.Annotations;
using Grove.Workflows.Documents;
using YesSql.Indexes;

namespace Grove.Workflows.Indexes;

[UsedImplicitly]
public class StoredBookmarkIndexProvider : IndexProvider<StoredBookmarkDocument>
{
    public StoredBookmarkIndexProvider()
    {
        CollectionName = WorkflowCollections.StoredBookmarks;
    }

    public override void Describe(DescribeContext<StoredBookmarkDocument> context)
    {
        context.For<StoredBookmarkIndex>().Map(document => new()
        {
            BookmarkId = document.BookmarkId,
            Name = document.Name,
            WorkflowInstanceId = document.WorkflowInstanceId,
            CorrelationId = document.CorrelationId,
            ActivityInstanceId = document.ActivityInstanceId,
            Hash = document.Hash,
        });
    }
}