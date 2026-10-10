using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.CommitStates;
using Crest.Workflows.Management;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Notifications;
using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.State;

namespace Crest.Workflows.Runtime;

public class DefaultCommitStateHandler(
    IWorkflowInstanceManager workflowInstanceManager,
    IBookmarksPersister bookmarkPersister,
    IVariablePersistenceManager variablePersistenceManager,
    IMediator mediator,
    ILogRecordSink<ActivityExecutionRecord> activityExecutionLogRecordSink,
    ILogRecordSink<WorkflowExecutionLogRecord> workflowExecutionLogRecordSink) : ICommitStateHandler
{
    public async Task CommitAsync(WorkflowExecutionContext workflowExecutionContext, CancellationToken cancellationToken = default)
    {
        var workflowState = workflowInstanceManager.ExtractWorkflowState(workflowExecutionContext);
        await CommitAsync(workflowExecutionContext, workflowState, cancellationToken);
    }

    public async Task CommitAsync(WorkflowExecutionContext workflowExecutionContext, WorkflowState workflowState, CancellationToken cancellationToken = default)
    {
        var updateBookmarksRequest = new UpdateBookmarksRequest(workflowExecutionContext, workflowExecutionContext.BookmarksDiff, workflowExecutionContext.CorrelationId);
        await bookmarkPersister.PersistBookmarksAsync(updateBookmarksRequest);
        await activityExecutionLogRecordSink.PersistExecutionLogsAsync(workflowExecutionContext, cancellationToken);
        await workflowExecutionLogRecordSink.PersistExecutionLogsAsync(workflowExecutionContext, cancellationToken);
        await variablePersistenceManager.SaveVariablesAsync(workflowExecutionContext);
        var workflowInstance = await workflowInstanceManager.SaveAsync(workflowState, cancellationToken);
        workflowExecutionContext.ExecutionLog.Clear();
        workflowExecutionContext.ClearCompletedActivityExecutionContexts();
        await workflowExecutionContext.ExecuteDeferredTasksAsync();
        await mediator.SendAsync(new WorkflowStateCommitted(workflowExecutionContext, workflowState, workflowInstance), cancellationToken);
    }
}