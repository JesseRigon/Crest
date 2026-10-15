using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Activities;
using Grove.Workflows.Management.Models;

namespace Grove.Workflows.Management.Notifications;

/// <summary>
/// A request to validate a workflow definition.
/// </summary>
/// <param name="Workflow">The workflow materialized from the definition.</param>
/// <param name="ValidationErrors">The collection of validation errors.</param>
public record WorkflowDefinitionValidating(Workflow Workflow, ICollection<WorkflowValidationError> ValidationErrors) : INotification;