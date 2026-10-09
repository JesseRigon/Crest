using Crest.Users.Events;
using Crest.Users.Models;
using Crest.Users.Workflows.Activities;
using Crest.Workflows.Platform.Services;

namespace Crest.Users.Workflows.Handlers;

public sealed class LogoutFormEventHandler : LogoutFormEventBase
{
    private readonly IWorkflowManager _workflowManager;

    public LogoutFormEventHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    /// <inheritdoc/>
    public override Task LoggedOutAsync(IUser user, CancellationToken cancellationToken = default)
    {
        if (user is not User u)
        {
            return Task.CompletedTask;
        }

        var input = new Dictionary<string, object>
        {
            ["UserName"] = u.UserName,
            ["Roles"] = u.RoleNames,
        };

        return _workflowManager.TriggerEventAsync(
            name: nameof(UserLoggedOutEvent),
            input: input,
            correlationId: u.UserId);
    }
}
