using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Activities;
using Grove.Workflows.State;

namespace Grove.Workflows.Notifications;

/// <summary>
/// A domain event that is published everytime a burst of execution completes.  
/// </summary>
public record WorkflowExecuted(Workflow Workflow, WorkflowState WorkflowState, WorkflowExecutionContext WorkflowExecutionContext) : INotification;