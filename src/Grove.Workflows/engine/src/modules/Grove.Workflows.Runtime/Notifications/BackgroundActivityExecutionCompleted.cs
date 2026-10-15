using Grove.Workflows.Mediator.Contracts;

namespace Grove.Workflows.Runtime.Notifications;

public record BackgroundActivityExecutionCompleted(ActivityExecutionContext ActivityExecutionContext) : INotification;