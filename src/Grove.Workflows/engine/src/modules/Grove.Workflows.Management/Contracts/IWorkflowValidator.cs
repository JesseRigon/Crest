using Grove.Workflows.Activities;
using Grove.Workflows.Management.Models;

namespace Grove.Workflows.Management;

/// <summary>
/// Validates a workflow definition.
/// </summary>
public interface IWorkflowValidator
{
    /// <summary>
    /// Validates a workflow.
    /// </summary>
    /// <param name="workflow">The workflow to validate.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of validation errors.</returns>
    Task<IEnumerable<WorkflowValidationError>> ValidateAsync(Workflow workflow, CancellationToken cancellationToken = default);
}