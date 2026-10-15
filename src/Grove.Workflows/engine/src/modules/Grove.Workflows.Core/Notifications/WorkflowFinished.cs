using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Activities;
using Grove.Workflows.State;

namespace Grove.Workflows.Notifications;

/// <summary>
/// A domain event that is published when a workflow finishes.
/// </summary>
public record WorkflowFinished(Workflow Workflow, WorkflowState WorkflowState, WorkflowExecutionContext WorkflowExecutionContext) : INotification;