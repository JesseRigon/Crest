using Crest.Users.Handlers;
using Crest.Users.Models;
using Crest.Workflows;

namespace Crest.Users.Workflows.Handlers;

/// <summary>Raises the user triggers after the unit that changed the user commits.</summary>
public class UserEventHandler(IWorkflowTriggerPublisher triggers) : UserEventHandlerBase
{
    public override Task CreatedAsync(UserCreateContext context) => RaiseAsync(UserWorkflowTriggers.Created, context.User);

    public override Task DeletedAsync(UserDeleteContext context) => RaiseAsync(UserWorkflowTriggers.Deleted, context.User);

    public override Task DisabledAsync(UserContext context) => RaiseAsync(UserWorkflowTriggers.Disabled, context.User);

    public override Task EnabledAsync(UserContext context) => RaiseAsync(UserWorkflowTriggers.Enabled, context.User);

    public override Task UpdatedAsync(UserUpdateContext context) => RaiseAsync(UserWorkflowTriggers.Updated, context.User);

    public override Task ConfirmedAsync(UserConfirmContext context) => RaiseAsync(UserWorkflowTriggers.Confirmed, context.User);

    private Task RaiseAsync(string trigger, IUser user)
        => user is User platformUser
            ? triggers.PublishAsync(trigger, platformUser.UserId, UserWorkflowTriggers.Payload(platformUser))
            : Task.CompletedTask;
}
