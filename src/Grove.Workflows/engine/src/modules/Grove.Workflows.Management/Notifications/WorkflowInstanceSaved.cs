using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Management.Entities;

namespace Grove.Workflows.Management.Notifications;

/// <summary>
/// A notification that is sent when a workflow instance is saved.
/// </summary>
/// <param name="WorkflowInstance">The workflow instance.</param>
public record WorkflowInstanceSaved(WorkflowInstance WorkflowInstance) : INotification;