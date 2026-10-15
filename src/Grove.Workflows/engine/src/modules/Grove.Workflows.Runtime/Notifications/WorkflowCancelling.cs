using Grove.Workflows.Mediator.Contracts;

namespace Grove.Workflows.Runtime.Notifications;

public record WorkflowCancelling(string WorkflowInstanceId) : INotification;