using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Notifications;

namespace Grove.Workflows.Runtime.Handlers;

public class EvaluateParentLogPersistenceModes(IActivityPropertyLogPersistenceEvaluator persistenceEvaluator) : INotificationHandler<InvokingActivityCallback>
{
    public async Task HandleAsync(InvokingActivityCallback notification, CancellationToken cancellationToken)
    {
        // Before invoking the parent activity, make sure its persistence log properties are evaluated.
        if (!notification.Parent.HasLogPersistenceModeMap())
        {
            var persistenceLogMap = await persistenceEvaluator.EvaluateLogPersistenceModesAsync(notification.Parent);
            notification.Parent.SetLogPersistenceModeMap(persistenceLogMap);
        }
    }
}