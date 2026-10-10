using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Responses;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// A notification that is published when a workflow instance has been dispatched.
/// </summary>
public record WorkflowInstanceDispatched(DispatchWorkflowInstanceRequest Request, DispatchWorkflowResponse Response) : INotification;
