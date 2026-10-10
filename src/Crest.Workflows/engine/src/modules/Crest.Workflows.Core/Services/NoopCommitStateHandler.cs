using Crest.Workflows.CommitStates;
using Crest.Workflows.State;

namespace Crest.Workflows;

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