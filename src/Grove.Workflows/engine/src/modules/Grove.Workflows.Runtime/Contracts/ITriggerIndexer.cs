using Grove.Workflows.Activities;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Runtime.Entities;
using Grove.Workflows.Runtime.Filters;
using Grove.Workflows.Runtime.Notifications;

namespace Grove.Workflows.Runtime;

/// <summary>
/// Extracts triggers from workflow definitions.
/// </summary>
public interface ITriggerIndexer
{
    /// <summary>
    /// Removes triggers matching the specified filter.
    /// </summary>
    Task DeleteTriggersAsync(TriggerFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes triggers of the specified workflow.
    /// </summary>
    Task<IndexedWorkflowTriggers> IndexTriggersAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Indexes triggers of the specified workflow.
    /// </summary>
    Task<IndexedWorkflowTriggers> IndexTriggersAsync(Workflow workflow, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Returns triggers for the specified workflow definition.
    /// </summary>
    /// <param name="workflow">The workflow.</param>
    /// <param name="cancellationToken">An optional cancellation token.</param>
    /// <returns>A collection of triggers.</returns>
    Task<IEnumerable<StoredTrigger>> GetTriggersAsync(Workflow workflow, CancellationToken cancellationToken = default);
}