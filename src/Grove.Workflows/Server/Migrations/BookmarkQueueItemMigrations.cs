using JetBrains.Annotations;
using Grove.Data.Migration;
using Grove.Workflows.Indexes;
using YesSql.Sql;

namespace Grove.Workflows.Migrations;

[UsedImplicitly]
public class BookmarkQueueItemMigrations : DataMigration
{
    private const string Collection = WorkflowCollections.BookmarkQueue;

    [UsedImplicitly]
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<BookmarkQueueItemIndex>(table => table
            .Column<string>(nameof(BookmarkQueueItemIndex.ItemId), c => c.NotNull())
            .Column<string>(nameof(BookmarkQueueItemIndex.WorkflowInstanceId), c => c.Nullable())
            .Column<string>(nameof(BookmarkQueueItemIndex.BookmarkId), c => c.Nullable())
            .Column<string>(nameof(BookmarkQueueItemIndex.StimulusHash), c => c.Nullable())
            .Column<string>(nameof(BookmarkQueueItemIndex.ActivityInstanceId), c => c.Nullable())
            .Column<string>(nameof(BookmarkQueueItemIndex.ActivityTypeName), c => c.Nullable())
            .Column<DateTimeOffset>(nameof(BookmarkQueueItemIndex.CreatedAt), c => c.NotNull())
        , Collection);

        await SchemaBuilder.AlterIndexTableAsync<BookmarkQueueItemIndex>(table =>
        {
            table.CreateIndex("IDX_BookmarkQueueItemIndex_ItemId", nameof(BookmarkQueueItemIndex.ItemId));
            table.CreateIndex("IDX_BookmarkQueueItemIndex_WorkflowInstanceId", nameof(BookmarkQueueItemIndex.WorkflowInstanceId));
            table.CreateIndex("IDX_BookmarkQueueItemIndex_StimulusHash", nameof(BookmarkQueueItemIndex.StimulusHash));
            table.CreateIndex("IDX_BookmarkQueueItemIndex_CreatedAt", nameof(BookmarkQueueItemIndex.CreatedAt));
        }, Collection);

        return 1;
    }
}
