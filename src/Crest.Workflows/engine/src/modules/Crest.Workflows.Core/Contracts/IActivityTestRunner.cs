using Crest.Workflows.Models;

namespace Crest.Workflows;

public interface IActivityTestRunner
{
    Task<ActivityExecutionContext> RunAsync(WorkflowGraph workflowGraph, IActivity activity, CancellationToken cancellationToken = default);
}