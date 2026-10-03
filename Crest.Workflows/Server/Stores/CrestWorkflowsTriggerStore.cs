using Crest.Workflows.Common.Entities;
using Crest.Workflows.Common.Models;
using Crest.Workflows;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.OrderDefinitions;
using Open.Linq.AsyncExtensions;
using Crest.Workflows.Documents;
using Crest.Workflows.Indexes;
using Crest.Workflows.Extensions;
using YesSql;

namespace Crest.Workflows.Stores;

public class CrestWorkflowsTriggerStore(ISession session, IPayloadSerializer payloadSerializer) : ITriggerStore
{
    private const string Collection = CrestWorkflowsCollections.StoredTriggers;

    public async ValueTask SaveAsync(StoredTrigger record, CancellationToken cancellationToken = default)
    {
        var document = await Query(new() { Ids = new List<string> { record.Id } }).FirstOrDefaultAsync(cancellationToken);
        document = Map(document, record);
        await session.SaveAsync(document, collection: Collection);
        await session.FlushAsync(cancellationToken);
    }

    public async ValueTask SaveManyAsync(IEnumerable<StoredTrigger> records, CancellationToken cancellationToken = default)
    {
        foreach (var record in records)
        {
            var document = await Query(new() { Ids = new List<string> { record.Id } }).FirstOrDefaultAsync(cancellationToken);
            document = Map(document, record);
            await session.SaveAsync(document, collection: Collection);
        }

        await session.FlushAsync(cancellationToken);
    }

    public async ValueTask<StoredTrigger?> FindAsync(TriggerFilter filter, CancellationToken cancellationToken = default)
    {
        var document = await Query(filter).FirstOrDefaultAsync(cancellationToken);
        return Map(document);
    }

    public async ValueTask<IEnumerable<StoredTrigger>> FindManyAsync(TriggerFilter filter, CancellationToken cancellationToken = default)
    {
        var documents = await Query(filter).ListAsync(cancellationToken);
        return Map(documents);
    }

    public ValueTask<Page<StoredTrigger>> FindManyAsync(TriggerFilter filter, PageArgs pageArgs, CancellationToken cancellationToken = default)
    {
        return FindManyAsync(filter, pageArgs, new StoredTriggerOrder<string>(x => x.Id, OrderDirection.Ascending), cancellationToken);
    }

    public async ValueTask<Page<StoredTrigger>> FindManyAsync<TProp>(TriggerFilter filter, PageArgs pageArgs, StoredTriggerOrder<TProp> order, CancellationToken cancellationToken = default)
    {
        var query = Query(filter, order, pageArgs);
        var count = await query.CountAsync(cancellationToken);
        var documents = (await query.ListAsync(cancellationToken)).ToList();

        return Page.Of(Map(documents).ToList(), count);
    }

    public async ValueTask ReplaceAsync(IEnumerable<StoredTrigger> removed, IEnumerable<StoredTrigger> added, CancellationToken cancellationToken = default)
    {
        // A trigger in both lists (re-indexing keeps ids) is an update, never a delete followed
        // by an insert of the same document: within one session (the burst commits as one, see
        // the units in CoreStartup) YesSql refuses that pair at flush time.
        var addedList = added.ToList();
        var kept = new HashSet<string>(addedList.Select(a => a.Id), StringComparer.Ordinal);
        var removedIds = removed.Select(r => r.Id).Where(id => !kept.Contains(id)).ToList();

        if (removedIds.Count > 0)
        {
            await DeleteManyAsync(new TriggerFilter { Ids = removedIds }, cancellationToken);
        }

        await SaveManyAsync(addedList, cancellationToken);
    }

    public async ValueTask<long> DeleteManyAsync(TriggerFilter filter, CancellationToken cancellationToken = default)
    {
        var pageArgs = PageArgs.FromRange(0, 100);
        var count = 0;

        while (true)
        {
            var query = Query(filter).OrderBy(x => x.TriggerId).Skip(pageArgs.Offset!.Value).Take(pageArgs.Limit!.Value);
            var documents = (await query.ListAsync(cancellationToken)).ToList();
            count += documents.Count;

            if (documents.Count == 0)
                break;

            foreach (var document in documents)
                session.Delete(document, collection: Collection);

            pageArgs = pageArgs.Next();
        }

        await session.FlushAsync(cancellationToken);
        return count;
    }

    private IQuery<StoredTriggerDocument, StoredTriggerIndex> Query(TriggerFilter filter)
    {
        return session.Query<StoredTriggerDocument, StoredTriggerIndex>(collection: Collection).Apply(filter);
    }

    private IQuery<StoredTriggerDocument, StoredTriggerIndex> Query<TOrderBy>(TriggerFilter filter, StoredTriggerOrder<TOrderBy>? order = null, PageArgs? pageArgs = null)
    {
        var query = session.Query<StoredTriggerDocument, StoredTriggerIndex>(collection: Collection).Apply(filter);
        if (order != null) query = query.Apply(order);

        if (pageArgs != null)
        {
            if (pageArgs.Offset != null) query.Skip(pageArgs.Offset.Value);
            if (pageArgs.Limit != null) query.Take(pageArgs.Limit.Value);
        }

        return query;
    }

    private StoredTriggerDocument Map(StoredTriggerDocument? target, StoredTrigger source)
    {
        if (target == null)
            target = new();

        target.TriggerId = source.Id;
        target.TenantId = source.TenantId;
        target.WorkflowDefinitionId = source.WorkflowDefinitionId;
        target.WorkflowDefinitionVersionId = source.WorkflowDefinitionVersionId;
        target.Name = source.Name;
        target.ActivityId = source.ActivityId;
        target.Hash = source.Hash;

        if (source.Payload != null)
            target.SerializedPayload = payloadSerializer.Serialize(source.Payload);

        return target;
    }

    private IEnumerable<StoredTrigger> Map(IEnumerable<StoredTriggerDocument> source)
    {
        return source.Select(x => Map(x)!);
    }

    private StoredTrigger? Map(StoredTriggerDocument? source)
    {
        if (source == null)
            return null;

        var payload = source.SerializedPayload != null
            ? payloadSerializer.Deserialize(source.SerializedPayload)
            : null;

        return new()
        {
            Id = source.TriggerId,
            TenantId = source.TenantId,
            WorkflowDefinitionId = source.WorkflowDefinitionId,
            WorkflowDefinitionVersionId = source.WorkflowDefinitionVersionId,
            Name = source.Name,
            ActivityId = source.ActivityId,
            Hash = source.Hash,
            Payload = payload
        };
    }
}
