using Crest.Workflows.Activities;
using Crest.Workflows.Models;
using Crest.Workflows.Options;

namespace Crest.Workflows.Runtime;

/// <inheritdoc />
public class WorkflowInvoker(IWorkflowGraphBuilder workflowGraphBuilder, IWorkflowRunner workflowRunner) : IWorkflowInvoker
{
    /// <inheritdoc />
    public async Task<RunWorkflowResult> InvokeAsync(Workflow workflow, RunWorkflowOptions? options = default, CancellationToken cancellationToken = default)
    {
        var workflowGraph = await workflowGraphBuilder.BuildAsync(workflow, cancellationToken);
        return await InvokeAsync(workflowGraph, options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RunWorkflowResult> InvokeAsync(WorkflowGraph workflowGraph, RunWorkflowOptions? options = default, CancellationToken cancellationToken = default)
    {
        return await workflowRunner.RunAsync(workflowGraph, options, cancellationToken);
    }
}