using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Management.Notifications;
using Grove.Workflows.Runtime.Filters;
using JetBrains.Annotations;

namespace Grove.Workflows.Runtime.Handlers;

/// <summary>
/// Deletes activity execution log records in response to the <see cref="WorkflowInstancesDeleting"/> notification.
/// </summary>
[PublicAPI]
internal class DeleteActivityExecutionLogRecords : INotificationHandler<WorkflowInstancesDeleting>
{
    private readonly IActivityExecutionStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteActivityExecutionLogRecords"/> class.
    /// </summary>
    public DeleteActivityExecutionLogRecords(IActivityExecutionStore store)
    {
        _store = store;
    }
    
    /// <inheritdoc />
    public async Task HandleAsync(WorkflowInstancesDeleting notification, CancellationToken cancellationToken)
    {
        await DeleteManyAsync(new ActivityExecutionRecordFilter { WorkflowInstanceIds = notification.Ids }, cancellationToken);
    }
    
    private async Task DeleteManyAsync(ActivityExecutionRecordFilter filter, CancellationToken cancellationToken = default)
    {
        await _store.DeleteManyAsync(filter, cancellationToken);
    }
}