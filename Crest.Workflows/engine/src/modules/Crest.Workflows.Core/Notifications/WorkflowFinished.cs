using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Activities;
using Crest.Workflows.State;

namespace Crest.Workflows.Notifications;

/// <summary>
/// A domain event that is published when a workflow finishes.
/// </summary>
public record WorkflowFinished(Workflow Workflow, WorkflowState WorkflowState, WorkflowExecutionContext WorkflowExecutionContext) : INotification;