using Crest.Workflows.Mediator.Contracts;

namespace Crest.Workflows.Runtime.Notifications;

public record WorkflowCancelled(string WorkflowInstanceId) : INotification;