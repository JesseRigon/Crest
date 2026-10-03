using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Responses;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// A notification that is published when a workflow definition has been dispatched.
/// </summary>
public record WorkflowDefinitionDispatched(DispatchWorkflowDefinitionRequest Request, DispatchWorkflowResponse Response) : INotification;
