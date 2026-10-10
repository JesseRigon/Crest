using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Activities;

namespace Crest.Workflows.Notifications;

/// <summary>
/// A domain event that is published when a workflow starts.
/// </summary>
public record WorkflowStarted(Workflow Workflow, WorkflowExecutionContext WorkflowExecutionContext) : INotification;