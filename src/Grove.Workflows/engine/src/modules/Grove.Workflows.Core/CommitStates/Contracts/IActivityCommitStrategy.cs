namespace Grove.Workflows.CommitStates;

public interface IActivityCommitStrategy
{
    CommitAction ShouldCommit(ActivityCommitStateStrategyContext context);
}