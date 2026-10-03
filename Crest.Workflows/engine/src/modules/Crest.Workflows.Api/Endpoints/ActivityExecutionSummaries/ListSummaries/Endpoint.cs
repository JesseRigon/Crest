using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Models;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.OrderDefinitions;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.ActivityExecutionSummaries.ListSummaries;

/// <summary>
/// Lists a summary view of the executions for a given activity.
/// </summary>
[PublicAPI]
internal class Endpoint(IActivityExecutionStore store) : CrestWorkflowsEndpoint<Request, ListResponse<ActivityExecutionRecordSummary>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("/activity-execution-summaries/list");
        ConfigurePermissions("read:activity-execution");
    }

    /// <inheritdoc />
    public override async Task<ListResponse<ActivityExecutionRecordSummary>> ExecuteAsync(Request request, CancellationToken cancellationToken)
    {
        var filter = new ActivityExecutionRecordFilter
        {
            WorkflowInstanceId = request.WorkflowInstanceId,
            ActivityNodeId = request.ActivityNodeId,
            Completed = request.Completed
        };
        var order = new ActivityExecutionRecordOrder<DateTimeOffset>(x => x.StartedAt, OrderDirection.Ascending);
        var records = (await store.FindManySummariesAsync(filter, order, cancellationToken)).ToList();
        return new ListResponse<ActivityExecutionRecordSummary>(records);
    }
}