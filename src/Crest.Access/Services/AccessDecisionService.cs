using Crest.Security.Permissions;

namespace Crest.Access.Services;

/// <summary>
/// The one decision: ceilings first (a single deny wins), then the super user, then the
/// resource mapping, then the caller's permissions with the implied-by chain resolved.
/// </summary>
public sealed class AccessDecisionService(
    IPermissionService permissions,
    IEnumerable<IAccessCeiling> ceilings,
    IEnumerable<IResourcePermissionMapper> mappers) : IAccessDecision
{
    public async Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object? resource = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrEmpty(permission);

        foreach (var ceiling in ceilings)
        {
            if (ceiling.Deny(caller, permission) is { } reason)
            {
                return AccessDecision.Ceilinged(reason);
            }
        }

        if (caller.IsSuperUser)
        {
            return AccessDecision.Allowed;
        }

        IReadOnlyList<string> candidates = [permission];
        if (resource is not null)
        {
            foreach (var mapper in mappers)
            {
                if (mapper.Map(permission, resource, caller) is { } mapped)
                {
                    candidates = mapped;
                    break;
                }
            }
        }

        foreach (var candidate in candidates)
        {
            if (await IsGrantedAsync(caller, candidate))
            {
                return AccessDecision.Allowed;
            }
        }

        return AccessDecision.Denied($"'{permission}' is not granted.");
    }

    private async Task<bool> IsGrantedAsync(CallerContext caller, string permissionName)
    {
        if (caller.Permissions.Contains(permissionName))
        {
            return true;
        }

        var permission = await permissions.FindByNameAsync(permissionName);
        if (permission is null)
        {
            return false;
        }

        var granting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Collect(permission, granting);
        return granting.Overlaps(caller.Permissions);
    }

    private static void Collect(Permission permission, HashSet<string> granting)
    {
        if (!granting.Add(permission.Name))
        {
            return;
        }

        if (permission.ImpliedBy is null)
        {
            return;
        }

        foreach (var implied in permission.ImpliedBy)
        {
            if (implied is not null)
            {
                Collect(implied, granting);
            }
        }
    }
}
