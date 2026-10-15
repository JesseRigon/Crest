using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Activities;

namespace Grove.Workflows.Notifications;

/// <summary>
/// A domain event that is published before a burst of execution begins.  
/// </summary>
public record WorkflowExecuting(Workflow Workflow, WorkflowExecutionContext WorkflowExecutionContext) : INotification;