using Crest.Workflows.Mediator.Contracts;

namespace Crest.Workflows.Runtime.Notifications;

public record BackgroundActivityExecutionCompleted(ActivityExecutionContext ActivityExecutionContext) : INotification;