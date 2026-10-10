using Crest.Workflows.Management.Entities;
using Crest.Workflows.Models;

namespace Crest.Workflows.Management.Models;

/// <summary>
/// Represents the result of updating workflow references.
/// </summary>
public class UpdateWorkflowReferencesResult(IEnumerable<WorkflowDefinition> updatedWorkflows)
{
    /// <summary>
    /// Gets a collection of workflow graphs that have been updated.
    /// </summary>
    /// <value>
    /// A read-only collection of <see cref="WorkflowGraph"/> instances representing the updated workflows.
    /// </value>
    public IReadOnlyCollection<WorkflowDefinition> UpdatedWorkflows { get; } = updatedWorkflows.ToList();
}