namespace Grove.Workflows.CommitStates;

public interface IWorkflowCommitStrategy
{
    CommitAction ShouldCommit(WorkflowCommitStateStrategyContext context);
}