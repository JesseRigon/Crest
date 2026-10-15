using Grove.Workflows.Extensions;
using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Notifications;
using JetBrains.Annotations;

namespace Grove.Workflows.Runtime.Handlers;

/// <summary>
/// Captures the execution state of an activity when it completes.
/// </summary>
[UsedImplicitly]
public class CaptureActivityExecutionState : INotificationHandler<ActivityCompleted>
{
    public async Task HandleAsync(ActivityCompleted notification, CancellationToken cancellationToken)
    {
        var context = notification.ActivityExecutionContext;
        await context.CaptureActivityExecutionRecordAsync();
    }
}