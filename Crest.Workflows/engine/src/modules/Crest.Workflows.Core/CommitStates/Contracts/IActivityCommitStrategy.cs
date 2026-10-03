namespace Crest.Workflows.CommitStates;

public interface IActivityCommitStrategy
{
    CommitAction ShouldCommit(ActivityCommitStateStrategyContext context);
}