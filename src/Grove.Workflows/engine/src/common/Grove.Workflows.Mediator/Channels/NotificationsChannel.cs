using Grove.Workflows.Mediator.Abstractions;
using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Mediator.Middleware.Notification;

namespace Grove.Workflows.Mediator.Channels;

/// <inheritdoc cref="Grove.Workflows.Mediator.Contracts.INotificationsChannel" />
public class NotificationsChannel : ChannelBase<NotificationContext>, INotificationsChannel
{
}