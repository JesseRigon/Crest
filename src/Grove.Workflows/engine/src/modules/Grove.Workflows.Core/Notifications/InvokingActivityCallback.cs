using Grove.Workflows.Mediator.Contracts;

namespace Grove.Workflows.Notifications;

public record InvokingActivityCallback(ActivityExecutionContext Parent, ActivityExecutionContext Child) : INotification;