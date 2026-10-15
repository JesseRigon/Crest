using Grove.Workflows.CommitStates;
using Grove.Workflows.State;

namespace Grove.Workflows;

public class NoopCommitStateHandler : ICommitStateHandler
{
    public Task CommitAsync(WorkflowExecutionContext workflowExecutionContext, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task CommitAsync(WorkflowExecutionContext workflowExecutionContext, WorkflowState workflowState, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}