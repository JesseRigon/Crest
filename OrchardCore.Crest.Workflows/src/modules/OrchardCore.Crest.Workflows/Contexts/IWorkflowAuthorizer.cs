using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security.Permissions;

namespace OrchardCore.Crest.Workflows.Contexts;

/// <summary>
/// Evaluates an Orchard permission, by name, against a snapshotted user - through the
/// tenant's real <see cref="IAuthorizationService"/>, so every registered handler applies.
/// Unknown permission names and anonymous users are denied.
/// </summary>
public interface IWorkflowAuthorizer
{
    Task<bool> AuthorizeAsync(WorkflowUserContext? user, string permissionName, CancellationToken cancellationToken = default);
}

public sealed class WorkflowAuthorizer(IAuthorizationService authorizationService, IPermissionService permissionService) : IWorkflowAuthorizer
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

        return await authorizationService.AuthorizeAsync(user.ToPrincipal(), permission);
    }
}
