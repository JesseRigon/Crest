using Grove.Workflows.Common.Entities;
using Grove.Workflows.Common.Models;
using Grove.Workflows;
using Grove.Workflows.Management;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Management.Models;
using Open.Linq.AsyncExtensions;
using Grove.Workflows.Documents;
using Grove.Workflows.Indexes;
using Grove.Workflows.Extensions;
using YesSql;

namespace Grove.Workflows.Stores;

public class YesSqlWorkflowInstanceStore(ISession session, IWorkflowStateSerializer workflowStateSerializer) : IWorkflowInstanceStore
{
    private const string Collection = WorkflowCollections.WorkflowInstances;

    public async ValueTask<WorkflowInstance?> FindAsync(WorkflowInstanceFilter filter, CancellationToken cancellationToken = default)
    {
        var document = await Query(filter).FirstOrDefaultAsync(cancellationToken);
        return Map(document);
    }

    public async ValueTask<Page<WorkflowInstance>> FindManyAsync(WorkflowInstanceFilter filter, PageArgs pageArgs, CancellationToken cancellationToken = default)
    {
        var query = Query(filter, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var documents = (await query.ListAsync(cancellationToken)).ToList();

        return Page.Of(Map(documents).ToList(), count);
    }

    public async ValueTask<Page<WorkflowInstance>> FindManyAsync<TOrderBy>(WorkflowInstanceFilter filter, PageArgs pageArgs, WorkflowInstanceOrder<TOrderBy> order, CancellationToken cancellationToken = default)
    {
        var query = Query(filter, order, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var documents = (await query.ListAsync(cancellationToken)).ToList();

        return Page.Of(Map(documents).ToList(), count);
    }

    public async ValueTask<IEnumerable<WorkflowInstance>> FindManyAsync(WorkflowInstanceFilter filter, CancellationToken cancellationToken = default)
    {
        var documents = await Query(filter).ListAsync(cancellationToken);
        return Map(documents);
    }

    public async ValueTask<IEnumerable<WorkflowInstance>> FindManyAsync<TOrderBy>(WorkflowInstanceFilter filter, WorkflowInstanceOrder<TOrderBy> order, CancellationToken cancellationToken = default)
    {
        var documents = await Query(filter, order).ListAsync(cancellationToken);
        return Map(documents);
    }

    public async ValueTask<long> CountAsync(WorkflowInstanceFilter filter, CancellationToken cancellationToken = default)
    {
        return await Query(filter).CountAsync(cancellationToken);
    }

    public async ValueTask<IEnumerable<string>> FindManyIdsAsync(WorkflowInstanceFilter filter, CancellationToken cancellationToken = default)
    {
        var query = QueryIndex(filter);
        var indexes = (await query.ListAsync(cancellationToken)).ToList();
        return indexes.Select(x => x.InstanceId).ToList();
    }

    public async ValueTask<Page<string>> FindManyIdsAsync(WorkflowInstanceFilter filter, PageArgs pageArgs, CancellationToken cancellationToken = default)
    {
        var query = QueryIndex(filter, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var indexes = (await query.ListAsync(cancellationToken)).ToList();
        var instanceIds = indexes.Select(x => x.InstanceId).ToList();

        return Page.Of(instanceIds, count);
    }

    public async ValueTask<Page<string>> FindManyIdsAsync<TOrderBy>(WorkflowInstanceFilter filter, PageArgs pageArgs, WorkflowInstanceOrder<TOrderBy> order, CancellationToken cancellationToken = default)
    {
        var query = QueryIndex(filter, order, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var indexes = (await query.ListAsync(cancellationToken)).ToList();
        var instanceIds = indexes.Select(x => x.InstanceId).ToList();

        return Page.Of(instanceIds, count);
    }

    public async ValueTask<Page<WorkflowInstanceSummary>> SummarizeManyAsync(WorkflowInstanceFilter filter, PageArgs pageArgs, CancellationToken cancellationToken = default)
    {
        var query = QueryIndex(filter, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var indexes = (await query.ListAsync(cancellationToken)).ToList();
        var summaries = MapSummaries(indexes).ToList();

        return Page.Of(summaries, count);
    }

    public async ValueTask<Page<WorkflowInstanceSummary>> SummarizeManyAsync<TOrderBy>(WorkflowInstanceFilter filter, PageArgs pageArgs, WorkflowInstanceOrder<TOrderBy> order, CancellationToken cancellationToken = default)
    {
        var query = QueryIndex(filter, order, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var indexes = (await query.ListAsync(cancellationToken)).ToList();
        var summaries = MapSummaries(indexes).ToList();

        return Page.Of(summaries, count);
    }

    public async ValueTask<IEnumerable<WorkflowInstanceSummary>> SummarizeManyAsync(WorkflowInstanceFilter filter, CancellationToken cancellationToken = default)
    {
        var query = QueryIndex(filter);
        var indexes = (await query.ListAsync(cancellationToken)).ToList();
        return MapSummaries(indexes);
    }

    public async ValueTask<IEnumerable<WorkflowInstanceSummary>> SummarizeManyAsync<TOrder>(WorkflowInstanceFilter filter, WorkflowInstanceOrder<TOrder> order, CancellationToken cancellationToken = default)
    {
        var query = QueryIndex(filter, order);
        var indexes = (await query.ListAsync(cancellationToken)).ToList();
        return MapSummaries(indexes);
    }

    public async ValueTask SaveAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        var document = await session.Query<WorkflowInstanceDocument, WorkflowInstanceIndex>(collection: Collection).Where(x => x.InstanceId == instance.Id).FirstOrDefaultAsync(cancellationToken);
        document = Map(document, instance);
        await session.SaveAsync(document, collection: Collection);
        await session.FlushAsync(cancellationToken);
    }

    public async ValueTask AddAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        var document = Map(null, instance);
        await session.SaveAsync(document, collection: Collection);
        await session.FlushAsync(cancellationToken);
    }

    public async ValueTask UpdateAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
    {
        var document = await session.Query<WorkflowInstanceDocument, WorkflowInstanceIndex>(collection: Collection).Where(x => x.InstanceId == instance.Id).FirstOrDefaultAsync(cancellationToken);
        document = Map(document, instance);
        await session.SaveAsync(document, collection: Collection);
        await session.FlushAsync(cancellationToken);
    }

    public async ValueTask SaveManyAsync(IEnumerable<WorkflowInstance> instances, CancellationToken cancellationToken = default)
    {
        foreach (var instance in instances)
        {
            var document = await session.Query<WorkflowInstanceDocument, WorkflowInstanceIndex>(collection: Collection).Where(x => x.InstanceId == instance.Id).FirstOrDefaultAsync(cancellationToken);
            document = Map(document, instance);
            await session.SaveAsync(document, collection: Collection);
        }

        await session.FlushAsync(cancellationToken);
    }

    public async ValueTask<long> DeleteAsync(WorkflowInstanceFilter filter, CancellationToken cancellationToken = default)
    {
        var pageArgs = PageArgs.FromRange(0, 100); // Always the first page: see the flush below.
        var order = new WorkflowInstanceOrder<string>(x => x.Id, OrderDirection.Ascending);
        var count = 0;

        while (true)
        {
            var query = Query(filter, order, pageArgs);
            var documents = (await query.ListAsync(cancellationToken)).ToList();
            count += documents.Count;

            if (documents.Count == 0)
                break;

            foreach (var document in documents)
                session.Delete(document, collection: Collection);

            // The deleted batch is gone once flushed, so the next batch is again the first page.
            await session.FlushAsync(cancellationToken);
        }

        await session.FlushAsync(cancellationToken);
        return count;
    }

    public async Task UpdateUpdatedTimestampAsync(string workflowInstanceId, DateTimeOffset value, CancellationToken cancellationToken = default)
    {
        var document = await session.Query<WorkflowInstanceDocument, WorkflowInstanceIndex>(collection: Collection).Where(x => x.InstanceId == workflowInstanceId).FirstOrDefaultAsync(cancellationToken);
        if (document == null)
            return;
        document.UpdatedAt = value;
        await session.SaveAsync(document, collection: Collection);
        await session.FlushAsync(cancellationToken);
    }

    private IQuery<WorkflowInstanceDocument, WorkflowInstanceIndex> Query(WorkflowInstanceFilter filter, PageArgs? pageArgs = null)
    {
        return Query<string>(filter, null, pageArgs);
    }

    private IQuery<WorkflowInstanceDocument, WorkflowInstanceIndex> Query<TOrderBy>(WorkflowInstanceFilter filter, WorkflowInstanceOrder<TOrderBy>? order = null, PageArgs? pageArgs = null)
    {
        var query = session.Query<WorkflowInstanceDocument, WorkflowInstanceIndex>(collection: Collection).Apply(filter);
        if (order != null) query = query.Apply(order);

        if (pageArgs != null)
        {
            if (pageArgs.Offset != null) query.Skip(pageArgs.Offset.Value);
            if (pageArgs.Limit != null) query.Take(pageArgs.Limit.Value);
        }

        return query;
    }

    private IQueryIndex<WorkflowInstanceIndex> QueryIndex(WorkflowInstanceFilter filter, PageArgs? pageArgs = null)
    {
        return QueryIndex<string>(filter, null, pageArgs);
    }

    private IQueryIndex<WorkflowInstanceIndex> QueryIndex<TOrderBy>(WorkflowInstanceFilter filter, WorkflowInstanceOrder<TOrderBy>? order = null, PageArgs? pageArgs = null)
    {
        var query = session.QueryIndex<WorkflowInstanceIndex>(Collection).Apply(filter);
        if (order != null) query = query.Apply(order);

        if (pageArgs != null)
        {
            if (pageArgs.Offset != null) query.Skip(pageArgs.Offset.Value);
            if (pageArgs.Limit != null) query.Take(pageArgs.Limit.Value);
        }

        return query;
    }

    private IEnumerable<WorkflowInstanceSummary> MapSummaries(IEnumerable<WorkflowInstanceIndex> indexes)
    {
        return indexes.Select(MapSummary).ToList();
    }

    private WorkflowInstanceSummary MapSummary(WorkflowInstanceIndex index)
    {
        return new()
        {
            DefinitionId = index.DefinitionId,
            Name = index.Name,
            Version = index.Version,
            Status = index.Status,
            SubStatus = index.SubStatus,
            DefinitionVersionId = index.DefinitionVersionId,
            Id = index.InstanceId,
            CorrelationId = index.CorrelationId,
            CreatedAt = index.CreatedAt,
            IncidentCount = index.IncidentCount,
            FinishedAt = index.FinishedAt,
            UpdatedAt = index.UpdatedAt
        };
    }

    private WorkflowInstanceDocument Map(WorkflowInstanceDocument? target, WorkflowInstance source)
    {
        if (target == null)
            target = new();

        target.InstanceId = source.Id;
        target.TenantId = source.TenantId;
        target.DefinitionId = source.DefinitionId;
        target.DefinitionVersionId = source.DefinitionVersionId;
        target.Version = source.Version;
        target.CorrelationId = source.CorrelationId;
        target.Name = source.Name;
        target.ParentWorkflowInstanceId = source.ParentWorkflowInstanceId;
        target.Status = source.Status;
        target.SubStatus = source.SubStatus;
        target.IncidentCount = source.IncidentCount;
        target.IsSystem = source.IsSystem;
        target.IsExecuting = source.IsExecuting;
        target.CreatedAt = source.CreatedAt;
        target.FinishedAt = source.FinishedAt;
        target.UpdatedAt = source.UpdatedAt;

        var workflowState = source.WorkflowState;
        var json = workflowStateSerializer.Serialize(workflowState);
        target.SerializedWorkflowState = json;
        
        return target;
    }

    private IEnumerable<WorkflowInstance> Map(IEnumerable<WorkflowInstanceDocument> source)
    {
        return source.Select(x => Map(x)!).ToList();
    }

    private WorkflowInstance? Map(WorkflowInstanceDocument? source)
    {
        if (source == null)
            return null;

        var workflowState = source.SerializedWorkflowState != null
            ? workflowStateSerializer.Deserialize(source.SerializedWorkflowState)
            : null;

        return new()
        {
            Id = source.InstanceId,
            TenantId = source.TenantId,
            DefinitionId = source.DefinitionId,
            DefinitionVersionId = source.DefinitionVersionId,
            Version = source.Version,
            CorrelationId = source.CorrelationId,
            Name = source.Name,
            ParentWorkflowInstanceId = source.ParentWorkflowInstanceId,
            Status = source.Status,
            SubStatus = source.SubStatus,
            IncidentCount = source.IncidentCount,
            IsSystem = source.IsSystem,
            IsExecuting = source.IsExecuting,
            CreatedAt = source.CreatedAt,
            FinishedAt = source.FinishedAt,
            UpdatedAt = source.UpdatedAt,
            WorkflowState = workflowState!
        };
    }
}