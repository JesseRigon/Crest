using Grove.Workflows.Models;

namespace Grove.Workflows;

public interface IActivityTestRunner
{
    Task<ActivityExecutionContext> RunAsync(WorkflowGraph workflowGraph, IActivity activity, CancellationToken cancellationToken = default);
}