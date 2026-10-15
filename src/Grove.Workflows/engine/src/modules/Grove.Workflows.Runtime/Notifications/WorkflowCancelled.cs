using Grove.Workflows.Mediator.Contracts;

namespace Grove.Workflows.Runtime.Notifications;

public record WorkflowCancelled(string WorkflowInstanceId) : INotification;