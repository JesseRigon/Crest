using Crest.Workflows.Mediator.Abstractions;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Mediator.Middleware.Notification;

namespace Crest.Workflows.Mediator.Channels;

/// <inheritdoc cref="Crest.Workflows.Mediator.Contracts.INotificationsChannel" />
public class NotificationsChannel : ChannelBase<NotificationContext>, INotificationsChannel
{
}