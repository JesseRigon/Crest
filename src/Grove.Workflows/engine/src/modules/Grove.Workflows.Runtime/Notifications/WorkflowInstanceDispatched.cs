using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Runtime.Requests;
using Grove.Workflows.Runtime.Responses;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// A notification that is published when a workflow instance has been dispatched.
/// </summary>
public record WorkflowInstanceDispatched(DispatchWorkflowInstanceRequest Request, DispatchWorkflowResponse Response) : INotification;
