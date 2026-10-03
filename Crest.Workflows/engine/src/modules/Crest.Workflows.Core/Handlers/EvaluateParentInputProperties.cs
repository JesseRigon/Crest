using Crest.Workflows.Extensions;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Notifications;

namespace Crest.Workflows.Handlers;

public class EvaluateParentInputProperties : INotificationHandler<InvokingActivityCallback>
{
    public async Task HandleAsync(InvokingActivityCallback notification, CancellationToken cancellationToken)
    {
        // Before invoking the parent activity, make sure its properties are evaluated.
        if (!notification.Parent.GetHasEvaluatedProperties()) 
            await notification.Parent.EvaluateInputPropertiesAsync();
    }
}