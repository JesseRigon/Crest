using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Activities;

namespace Grove.Workflows.Notifications;

/// <summary>
/// A domain event that is published when a workflow starts.
/// </summary>
public record WorkflowStarted(Workflow Workflow, WorkflowExecutionContext WorkflowExecutionContext) : INotification;