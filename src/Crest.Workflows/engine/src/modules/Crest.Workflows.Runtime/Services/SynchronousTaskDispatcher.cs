using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Notifications;

namespace Crest.Workflows.Runtime;

/// <summary>
/// Relies on the <see cref="INotificationSender"/> to synchronously publish the received request as a domain event.
/// </summary>
public class SynchronousTaskDispatcher : ITaskDispatcher
{
    private readonly INotificationSender _notificationSender;

    /// <summary>
    /// Constructor.
    /// </summary>
    public SynchronousTaskDispatcher(INotificationSender notificationSender) => _notificationSender = notificationSender;

    /// <inheritdoc />
    public async Task DispatchAsync(RunTaskRequest request, CancellationToken cancellationToken = default) => await _notificationSender.SendAsync(request, cancellationToken);
}