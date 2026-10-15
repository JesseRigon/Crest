using Grove.Workflows.Mediator.Contexts;
using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Mediator.Extensions;

namespace Grove.Workflows.Mediator.PublishingStrategies;

/// <summary>
/// Invokes event handlers in sequence and waits for the result.
/// </summary>
public class SequentialProcessingStrategy : IEventPublishingStrategy
{
    /// <inheritdoc />
    public async Task PublishAsync(NotificationStrategyContext context)
    {
        var notificationContext = context.NotificationContext;
        var notification = notificationContext.Notification;
        var notificationType = notification.GetType();
        var handleMethod = notificationType.GetNotificationHandlerMethod();
        
        foreach (var handler in context.Handlers) 
            await handler.InvokeAsync(handleMethod, context.NotificationContext);
    }
}