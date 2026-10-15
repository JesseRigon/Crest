using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Runtime.Requests;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// A notification that is published when a workflow instance is being dispatched.
/// </summary>
public record WorkflowInstanceDispatching(DispatchWorkflowInstanceRequest Request) : INotification;
