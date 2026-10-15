using Grove.Access.Users.Handlers;
using Grove.Access.Users.Models;
using Grove.Workflows;

namespace Grove.Access.Users.Workflows.Handlers;

/// <summary>Raises the user triggers after the unit that changed the user commits.</summary>
public class UserEventHandler(IWorkflowTriggerPublisher triggers) : UserEventHandlerBase
{
    // A sign-up and its e-mail confirmation come from an anonymous request: those flows act as
    // the user the event names (docs/access.md, Anonymous actors); an administrator stays the actor.
    public override Task CreatedAsync(UserCreateContext context) => RaiseForUserAsync(UserWorkflowTriggers.Created, context.User);

    public override Task DeletedAsync(UserDeleteContext context) => RaiseAsync(UserWorkflowTriggers.Deleted, context.User);

    public override Task DisabledAsync(UserContext context) => RaiseAsync(UserWorkflowTriggers.Disabled, context.User);

    public override Task EnabledAsync(UserContext context) => RaiseAsync(UserWorkflowTriggers.Enabled, context.User);

    public override Task UpdatedAsync(UserUpdateContext context) => RaiseAsync(UserWorkflowTriggers.Updated, context.User);

    public override Task ConfirmedAsync(UserConfirmContext context) => RaiseForUserAsync(UserWorkflowTriggers.Confirmed, context.User);

    private Task RaiseAsync(string trigger, IUser user)
        => user is User platformUser
            ? triggers.PublishAsync(trigger, platformUser.UserId, UserWorkflowTriggers.Payload(platformUser))
            : Task.CompletedTask;

    private Task RaiseForUserAsync(string trigger, IUser user)
        => user is User platformUser
            ? triggers.PublishForUserAsync(trigger, platformUser.UserId, platformUser.UserId, platformUser.UserName, UserWorkflowTriggers.Payload(platformUser))
            : Task.CompletedTask;
}
