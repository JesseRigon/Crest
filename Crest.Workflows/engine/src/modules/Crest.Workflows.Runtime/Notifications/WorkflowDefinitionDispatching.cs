using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Requests;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// A notification that is published when a workflow definition is being dispatched.
/// </summary>
public record WorkflowDefinitionDispatching(DispatchWorkflowDefinitionRequest Request) : INotification;
