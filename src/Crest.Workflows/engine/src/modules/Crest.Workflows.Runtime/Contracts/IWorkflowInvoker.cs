using Crest.Workflows.Activities;
using Crest.Workflows.Models;
using Crest.Workflows.Options;

namespace Crest.Workflows.Runtime;

/// <summary>
/// Invokes a workflow in a transactional manner (i.e., runs the workflow in the current context and not via the <see cref="IWorkflowRuntime"/>).
/// </summary>
public interface IWorkflowInvoker
{
    /// <summary>
    /// Invokes the workflow.
    /// </summary>
    Task<RunWorkflowResult> InvokeAsync(Workflow workflow, RunWorkflowOptions? options = default, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Invokes the workflow.
    /// </summary>
    Task<RunWorkflowResult> InvokeAsync(WorkflowGraph workflowGraph, RunWorkflowOptions? options = default, CancellationToken cancellationToken = default);
}