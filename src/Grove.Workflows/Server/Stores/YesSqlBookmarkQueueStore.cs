using Grove.Workflows.Common.Entities;
using Grove.Workflows.Common.Models;
using Grove.Workflows.Documents;
using Grove.Workflows.Indexes;
using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Entities;
using Grove.Workflows.Runtime.Filters;
using Grove.Workflows.Runtime.Options;
using Grove.Workflows.Runtime.OrderDefinitions;
using Open.Linq.AsyncExtensions;
using YesSql;
using YesSql.Services;

namespace Grove.Workflows.Stores;

/// <summary>
/// The bookmark queue in the tenant's store, replacing the engine's in-memory default: a
/// queued resume survives a restart and every node drains the same queue. The queue is
/// ordered by creation time only, which is the one order the engine asks for.
/// </summary>
public class YesSqlBookmarkQueueStore(ISession session, IPayloadSerializer payloadSerializer) : IBookmarkQueueStore
{
    private const string Collection = WorkflowCollections.BookmarkQueue;

    public async Task SaveAsync(BookmarkQueueItem record, CancellationToken cancellationToken = default)
    {
        var document = await Query(new() { Id = record.Id }).FirstOrDefaultAsync(cancellationToken);
        await session.SaveAsync(Map(document, record), collection: Collection);
        await session.FlushAsync(cancellationToken);
    }

    public async Task AddAsync(BookmarkQueueItem record, CancellationToken cancellationToken = default)
    {
        await session.SaveAsync(Map(null, record), collection: Collection);
        await session.FlushAsync(cancellationToken);
    }

    public async Task<BookmarkQueueItem?> FindAsync(BookmarkQueueFilter filter, CancellationToken cancellationToken = default)
        => Map(await Query(filter).FirstOrDefaultAsync(cancellationToken));

    public async Task<IEnumerable<BookmarkQueueItem>> FindManyAsync(BookmarkQueueFilter filter, CancellationToken cancellationToken = default)
        => (await Query(filter).ListAsync(cancellationToken)).Select(document => Map(document)!).ToList();

    public Task<Page<BookmarkQueueItem>> PageAsync<TOrderBy>(PageArgs pageArgs, BookmarkQueueItemOrder<TOrderBy> orderBy, CancellationToken cancellationToken = default)
        => PageAsync(pageArgs, new BookmarkQueueFilter(), orderBy, cancellationToken);

    public async Task<Page<BookmarkQueueItem>> PageAsync<TOrderBy>(PageArgs pageArgs, BookmarkQueueFilter filter, BookmarkQueueItemOrder<TOrderBy> orderBy, CancellationToken cancellationToken = default)
    {
        var count = await Query(filter).CountAsync(cancellationToken);
        var ordered = orderBy.Direction == OrderDirection.Descending
            ? Query(filter).OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            : Query(filter).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id);
        IQuery<BookmarkQueueItemDocument> query = ordered;

        if (pageArgs.Offset is { } offset)
        {
            query = query.Skip(offset);
        }

        if (pageArgs.Limit is { } limit)
        {
            query = query.Take(limit);
        }

        var documents = await query.ListAsync(cancellationToken);
        return Page.Of(documents.Select(document => Map(document)!).ToList(), count);
    }

    public async Task<long> DeleteAsync(BookmarkQueueFilter filter, CancellationToken cancellationToken = default)
    {
        var documents = (await Query(filter).ListAsync(cancellationToken)).ToList();
        foreach (var document in documents)
        {
            session.Delete(document, collection: Collection);
        }

        await session.FlushAsync(cancellationToken);
        return documents.Count;
    }

    private IQuery<BookmarkQueueItemDocument, BookmarkQueueItemIndex> Query(BookmarkQueueFilter filter)
    {
        var query = session.Query<BookmarkQueueItemDocument, BookmarkQueueItemIndex>(collection: Collection);
        if (filter.Id != null) query = query.Where(x => x.ItemId == filter.Id);
        if (filter.Ids != null) query = query.Where(x => x.ItemId.IsIn(filter.Ids));
        if (filter.BookmarkId != null) query = query.Where(x => x.BookmarkId == filter.BookmarkId);
        if (filter.BookmarkHash != null) query = query.Where(x => x.StimulusHash == filter.BookmarkHash);
        if (filter.ActivityInstanceId != null) query = query.Where(x => x.ActivityInstanceId == filter.ActivityInstanceId);
        if (filter.ActivityTypeName != null) query = query.Where(x => x.ActivityTypeName == filter.ActivityTypeName);
        if (filter.WorkflowInstanceId != null) query = query.Where(x => x.WorkflowInstanceId == filter.WorkflowInstanceId);
        if (filter.CreatedAtLessThan is { } before) query = query.Where(x => x.CreatedAt < before);
        return query;
    }

    private BookmarkQueueItemDocument Map(BookmarkQueueItemDocument? target, BookmarkQueueItem source)
    {
        target ??= new();
        target.ItemId = source.Id;
        target.TenantId = source.TenantId;
        target.WorkflowInstanceId = source.WorkflowInstanceId;
        target.CorrelationId = source.CorrelationId;
        target.BookmarkId = source.BookmarkId;
        target.StimulusHash = source.StimulusHash;
        target.ActivityInstanceId = source.ActivityInstanceId;
        target.ActivityTypeName = source.ActivityTypeName;
        target.SerializedOptions = source.Options is null ? null : payloadSerializer.Serialize(source.Options);
        target.CreatedAt = source.CreatedAt;
        return target;
    }

    private BookmarkQueueItem? Map(BookmarkQueueItemDocument? source) => source is null ? null : new()
    {
        Id = source.ItemId,
        TenantId = source.TenantId,
        WorkflowInstanceId = source.WorkflowInstanceId,
        CorrelationId = source.CorrelationId,
        BookmarkId = source.BookmarkId,
        StimulusHash = source.StimulusHash,
        ActivityInstanceId = source.ActivityInstanceId,
        ActivityTypeName = source.ActivityTypeName,
        Options = source.SerializedOptions is null ? null : payloadSerializer.Deserialize<ResumeBookmarkOptions>(source.SerializedOptions),
        CreatedAt = source.CreatedAt,
    };
}
