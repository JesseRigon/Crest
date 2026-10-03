using Crest.Workflows.Activities;
using Crest.Workflows.Models;

namespace Crest.Workflows;

/// <summary>
/// Builds a workflow graph from a workflow.
/// </summary>
public interface IWorkflowGraphBuilder
{
    /// <summary>
    /// Builds a workflow graph from a workflow.
    /// </summary>
    Task<WorkflowGraph> BuildAsync(Workflow workflow, CancellationToken cancellationToken = default);
}