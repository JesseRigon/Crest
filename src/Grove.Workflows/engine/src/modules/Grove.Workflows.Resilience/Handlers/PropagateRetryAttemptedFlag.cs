using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Resilience.Extensions;
using Grove.Workflows.Runtime.Notifications;
using JetBrains.Annotations;

namespace Grove.Workflows.Resilience.Handlers;

[UsedImplicitly]
public class PropagateRetryAttemptedFlag : INotificationHandler<BackgroundActivityExecutionCompleted>
{
    public Task HandleAsync(BackgroundActivityExecutionCompleted notification, CancellationToken cancellationToken)
    {
        var hasRetries = notification.ActivityExecutionContext.GetRetriesAttemptedFlag();

        if (hasRetries)
            notification.ActivityExecutionContext.SetRetriesAttemptedFlag(); // Propagates the flag to all ancestors.

        return Task.CompletedTask;
    }
}