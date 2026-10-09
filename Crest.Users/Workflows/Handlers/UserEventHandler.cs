using Crest.Users.Handlers;
using Crest.Users.Models;
using Crest.Users.Workflows.Activities;
using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Services;

namespace Crest.Users.Workflows.Handlers;

public class UserEventHandler : UserEventHandlerBase
{
    private readonly IWorkflowManager _workflowManager;

    public UserEventHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public override Task CreatedAsync(UserCreateContext context)
    {
        return TriggerWorkflowEventAsync(nameof(UserCreatedEvent), (User)context.User);
    }

    public override Task DeletedAsync(UserDeleteContext context)
    {
        return TriggerWorkflowEventAsync(nameof(UserDeletedEvent), (User)context.User);
    }

    public override Task DisabledAsync(UserContext context)
    {
        return TriggerWorkflowEventAsync(nameof(UserDisabledEvent), (User)context.User);
    }

    public override Task EnabledAsync(UserContext context)
    {
        return TriggerWorkflowEventAsync(nameof(UserEnabledEvent), (User)context.User);
    }

    public override Task UpdatedAsync(UserUpdateContext context)
    {
        return TriggerWorkflowEventAsync(nameof(UserUpdatedEvent), (User)context.User);
    }

    private Task<IEnumerable<WorkflowExecutionContext>> TriggerWorkflowEventAsync(string name, User user)
    {
        return _workflowManager.TriggerEventAsync(name,
            input: new { User = user },
            correlationId: user.UserId
        );
    }

    public override Task ConfirmedAsync(UserConfirmContext context)
        => TriggerWorkflowEventAsync(nameof(UserConfirmedEvent), (User)context.User);
}
