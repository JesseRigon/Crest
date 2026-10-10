using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Activities;
using Crest.Workflows.State;

namespace Crest.Workflows.Notifications;

/// <summary>
/// A domain event that is published everytime a burst of execution completes.  
/// </summary>
public record WorkflowExecuted(Workflow Workflow, WorkflowState WorkflowState, WorkflowExecutionContext WorkflowExecutionContext) : INotification;