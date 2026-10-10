using System.Threading.Channels;
using Crest.Workflows.Mediator.Middleware.Notification;

namespace Crest.Workflows.Mediator.Contracts;

/// <summary>
/// A channel that can be used to enqueue notifications.
/// </summary>
public interface INotificationsChannel
{
    /// <summary>
    /// Gets the writer for the notification queue.
    /// </summary>
    ChannelWriter<NotificationContext> Writer { get; }
    
    /// <summary>
    /// Gets the reader for the notification queue.
    /// </summary>
    ChannelReader<NotificationContext> Reader { get; }
}