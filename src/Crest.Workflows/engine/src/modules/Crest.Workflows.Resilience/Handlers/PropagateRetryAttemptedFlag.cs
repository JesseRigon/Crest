using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Resilience.Extensions;
using Crest.Workflows.Runtime.Notifications;
using JetBrains.Annotations;

namespace Crest.Workflows.Resilience.Handlers;

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