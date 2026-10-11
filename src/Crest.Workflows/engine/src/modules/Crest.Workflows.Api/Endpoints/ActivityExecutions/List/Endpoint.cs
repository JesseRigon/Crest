using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Models;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.OrderDefinitions;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.ActivityExecutions.List;

/// <summary>
/// Lists the executions for a given activity.
/// </summary>
[PublicAPI]
internal class List : WorkflowsEndpoint<Request, ListResponse<ActivityExecutionRecord>>
{
    private readonly IActivityExecutionStore _store;

    /// <inheritdoc />
    public List(IActivityExecutionStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Get("/activity-executions/list");
        ConfigurePermissions("read:activity-execution");
    }

    /// <inheritdoc />
    public override async Task<ListResponse<ActivityExecutionRecord>> ExecuteAsync(Request request, CancellationToken cancellationToken)
    {
        var filter = new ActivityExecutionRecordFilter
        {
            WorkflowInstanceId = request.WorkflowInstanceId,
            ActivityNodeId = request.ActivityNodeId,
            Completed = request.Completed
        };
        var order = new ActivityExecutionRecordOrder<DateTimeOffset>(x => x.StartedAt, OrderDirection.Ascending);
        var records = (await _store.FindManyAsync(filter, order, cancellationToken)).ToList();
        return new ListResponse<ActivityExecutionRecord>(records);
    }
}