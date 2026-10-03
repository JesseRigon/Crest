using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Requests;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// A notification that is published when a workflow instance is being dispatched.
/// </summary>
public record WorkflowInstanceDispatching(DispatchWorkflowInstanceRequest Request) : INotification;
