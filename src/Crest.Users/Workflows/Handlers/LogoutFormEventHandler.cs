using Crest.Users.Events;
using Crest.Users.Models;
using Crest.Workflows;

namespace Crest.Users.Workflows.Handlers;

/// <summary>Raises <c>user.logged-out</c> after the sign-out's unit commits.</summary>
public sealed class LogoutFormEventHandler(IWorkflowTriggerPublisher triggers) : LogoutFormEventBase
{
    /// <inheritdoc/>
    public override Task LoggedOutAsync(IUser user, CancellationToken cancellationToken = default)
        => user is User platformUser
            ? triggers.PublishAsync(UserWorkflowTriggers.LoggedOut, platformUser.UserId, UserWorkflowTriggers.Payload(platformUser), cancellationToken)
            : Task.CompletedTask;
}
