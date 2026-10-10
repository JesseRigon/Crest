using Crest.Access;
using Crest.Security.Permissions;

namespace Crest.Workflows.Contexts;

/// <summary>
/// Evaluates a Crest permission, by name, for a run's actor: the actor is rebuilt into a
/// caller with current rights and the one access decision answers. Unknown permission names,
/// a missing actor and the anonymous actor are denied.
/// </summary>
public interface IWorkflowAuthorizer
{
    Task<bool> AuthorizeAsync(WorkflowUserContext? user, string permissionName, CancellationToken cancellationToken = default);
}

public sealed class WorkflowAuthorizer(WorkflowCallerResolver callers, IAccessDecision decision, IPermissionService permissionService) : IWorkflowAuthorizer
{
    public async Task<bool> AuthorizeAsync(WorkflowUserContext? user, string permissionName, CancellationToken cancellationToken = default)
    {
        if (user is not { IsAuthenticated: true } || string.IsNullOrWhiteSpace(permissionName))
        {
            return false;
        }

        var permission = await permissionService.FindByNameAsync(permissionName.Trim());
        if (permission is null)
        {
            return false;
        }

        var caller = await callers.FromActorAsync(user, cancellationToken);
        return (await decision.DecideAsync(caller, permission.Name, cancellationToken: cancellationToken)).IsAllowed;
    }
}
