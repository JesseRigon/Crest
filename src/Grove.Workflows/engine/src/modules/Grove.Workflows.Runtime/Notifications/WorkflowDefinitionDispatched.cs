using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Runtime.Requests;
using Grove.Workflows.Runtime.Responses;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// A notification that is published when a workflow definition has been dispatched.
/// </summary>
public record WorkflowDefinitionDispatched(DispatchWorkflowDefinitionRequest Request, DispatchWorkflowResponse Response) : INotification;
