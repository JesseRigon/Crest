using Crest.Workflows.Abstractions;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Api.Models;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.OrderDefinitions;
using JetBrains.Annotations;

namespace Crest.Workflows.Api.Endpoints.WorkflowInstances.Journal.FilteredList;

/// <summary>
/// Gets the journal for a workflow instance.
/// </summary>
[PublicAPI]
internal class Get : CrestWorkflowsEndpoint<Request, Response>
{
    private readonly IWorkflowExecutionLogStore _store;

    /// <inheritdoc />
    public Get(IWorkflowExecutionLogStore store)
    {
        _store = store;
    }

    /// <inheritdoc />
    public override void Configure()
    {
        Post("/workflow-instances/{id}/journal");
        ConfigurePermissions("read:workflow-instances");
    }

    /// <inheritdoc />
    public override async Task<Response> ExecuteAsync(Request request, CancellationToken cancellationToken)
    {
        var pageArgs = PageArgs.From(request.Page, request.PageSize, request.Skip, request.Take);
        
        var filter = new WorkflowExecutionLogRecordFilter
        {
            WorkflowInstanceId = request.WorkflowInstanceId,
            ActivityIds = request.Filter?.ActivityIds,
            ActivityNodeIds = request.Filter?.ActivityNodeIds,
            ExcludeActivityTypes = request.Filter?.ExcludedActivityTypes,
            EventNames = request.Filter?.EventNames,
        };
        var order = new WorkflowExecutionLogRecordOrder<long>(x => x.Sequence, OrderDirection.Ascending);
        var pageOfRecords = await _store.FindManyAsync(filter, pageArgs, order, cancellationToken);

        var models = pageOfRecords.Items.Select(x =>
                new ExecutionLogRecord(
                    x.Id,
                    x.ActivityInstanceId,
                    x.ParentActivityInstanceId,
                    x.ActivityId,
                    x.ActivityType,
                    x.ActivityTypeVersion,
                    x.ActivityName,
                    x.ActivityNodeId,
                    x.Timestamp,
                    x.Sequence,
                    x.EventName,
                    x.Message,
                    x.Source,
                    x.ActivityState,
                    x.Payload))
            .ToList();

        return new(models, pageOfRecords.TotalCount);
    }
}