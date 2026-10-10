namespace Crest.Workflows.CommitStates;

public interface IWorkflowCommitStrategy
{
    CommitAction ShouldCommit(WorkflowCommitStateStrategyContext context);
}