using Crest.Workflows.Mediator.Contracts;

namespace Crest.Workflows.Notifications;

public record InvokingActivityCallback(ActivityExecutionContext Parent, ActivityExecutionContext Child) : INotification;