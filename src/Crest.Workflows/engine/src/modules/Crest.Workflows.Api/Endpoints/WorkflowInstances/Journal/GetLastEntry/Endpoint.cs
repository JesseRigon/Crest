using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.OrderDefinitions;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.WorkflowInstances.Journal.GetLastEntry;

/// <summary>
/// Return the last log entry for the specified workflow instance and activity ID.
/// </summary>
[PublicAPI]
public class Get(IWorkflowExecutionLogStore store) : WorkflowsEndpoint<Request, WorkflowExecutionLogRecord>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("/workflow-instances/{workflowInstanceId}/journal/{activityId}");
        ConfigurePermissions("read:workflow-instances");
    }

    /// <inheritdoc />
    public override async Task HandleAsync(Request request, CancellationToken cancellationToken)
    {
        var filter = new WorkflowExecutionLogRecordFilter
        {
            WorkflowInstanceId = request.WorkflowInstanceId,
            ActivityId = request.ActivityId,
            EventNames = ["Started", "Completed", "Faulted"]
        };

        var sort = new WorkflowExecutionLogRecordOrder<DateTimeOffset>(
            x => x.Timestamp,
            OrderDirection.Descending
        );

        var entry = await store.FindAsync(filter, sort, cancellationToken);

        if (entry == null)
        {
            await Send.NotFoundAsync(cancellationToken);
            return;
        }

        await Send.OkAsync(entry, cancellationToken);
    }
}