using Crest.Workflows.Common.Models;
using Crest.Workflows.Common.Services;
using Crest.Workflows.Extensions;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.OrderDefinitions;

namespace Crest.Workflows.Runtime.Stores;

/// <inheritdoc />
public class MemoryWorkflowExecutionLogStore : IWorkflowExecutionLogStore
{
    private readonly MemoryStore<WorkflowExecutionLogRecord> _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryWorkflowExecutionLogStore"/> class.
    /// </summary>
    public MemoryWorkflowExecutionLogStore(MemoryStore<WorkflowExecutionLogRecord> store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public Task AddAsync(WorkflowExecutionLogRecord record, CancellationToken cancellationToken = default)
    {
        _store.Add(record, x => x.Id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task AddManyAsync(IEnumerable<WorkflowExecutionLogRecord> records, CancellationToken cancellationToken = default)
    {
        _store.AddMany(records, x => x.Id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SaveAsync(WorkflowExecutionLogRecord record, CancellationToken cancellationToken = default)
    {
        _store.Save(record, x => x.Id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SaveManyAsync(IEnumerable<WorkflowExecutionLogRecord> records, CancellationToken cancellationToken = default)
    {
        _store.SaveMany(records, x => x.Id);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<WorkflowExecutionLogRecord?> FindAsync(WorkflowExecutionLogRecordFilter filter, CancellationToken cancellationToken = default)
    {
        var result = _store.Query(query => Filter(query, filter)).FirstOrDefault();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<WorkflowExecutionLogRecord?> FindAsync<TOrderBy>(WorkflowExecutionLogRecordFilter filter, WorkflowExecutionLogRecordOrder<TOrderBy> order, CancellationToken cancellationToken = default)
    {
        var result = _store.Query(query => Filter(query, filter).OrderBy(order)).FirstOrDefault();
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<Page<WorkflowExecutionLogRecord>> FindManyAsync(WorkflowExecutionLogRecordFilter filter, PageArgs pageArgs, CancellationToken cancellationToken = default)
    {
        var count = _store.Query(query => Filter(query, filter)).LongCount();
        var result = _store.Query(query => Filter(query, filter).Paginate(pageArgs)).ToList();
        return Task.FromResult(Page.Of(result, count));
    }

    /// <inheritdoc />
    public Task<Page<WorkflowExecutionLogRecord>> FindManyAsync<TOrderBy>(WorkflowExecutionLogRecordFilter filter, PageArgs pageArgs, WorkflowExecutionLogRecordOrder<TOrderBy> order, CancellationToken cancellationToken = default)
    {
        var count = _store.Query(query => Filter(query, filter)).LongCount();
        var result = _store.Query(query => Filter(query, filter).OrderBy(order).Paginate(pageArgs)).ToList();
        return Task.FromResult(Page.Of(result, count));
    }

    /// <inheritdoc />
    public Task<long> DeleteManyAsync(WorkflowExecutionLogRecordFilter filter, CancellationToken cancellationToken = default)
    {
        var records = _store.Query(query => Filter(query, filter)).ToList();
        _store.DeleteMany(records, x => x.Id);
        return Task.FromResult(records.LongCount());
    }

    private IQueryable<WorkflowExecutionLogRecord> Filter(IQueryable<WorkflowExecutionLogRecord> queryable, WorkflowExecutionLogRecordFilter filter) => filter.Apply(queryable);
}