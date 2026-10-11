using Crest.Access;
using Crest.Security.Permissions;

namespace Crest.Workflows.Contexts;

/// <summary>
/// Evaluates a permission, by name, for the burst's caller - the one the access gate set from
/// the run's sealed actor or the definition - through the one access decision. Unknown
/// permission names, no caller and the anonymous caller are denied.
/// </summary>
public interface IWorkflowAuthorizer
{
    Task<bool> AuthorizeAsync(string permissionName, CancellationToken cancellationToken = default);
}

public sealed class WorkflowAuthorizer(ICallerContextAccessor callers, IAccessDecision decision, IPermissionService permissionService) : IWorkflowAuthorizer
{
    public async Task<bool> AuthorizeAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        if (callers.Current is not { } caller || (!caller.IsAuthenticated && !caller.IsSystem) || string.IsNullOrWhiteSpace(permissionName))
        {
            return false;
        }

        var permission = await permissionService.FindByNameAsync(permissionName.Trim());
        if (permission is null)
        {
            return false;
        }

        return (await decision.DecideAsync(caller, permission.Name, cancellationToken: cancellationToken)).IsAllowed;
    }
}
