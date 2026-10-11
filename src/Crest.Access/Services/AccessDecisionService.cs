using Crest.Security.Permissions;

namespace Crest.Access.Services;

/// <summary>
/// The one decision: ceilings first (a single deny wins), then the super user, then the
/// resource mapping (a candidate may re-ask the question for another resource), then the
/// caller's permissions with the implied-by chain resolved. Every denial is recorded; an
/// allow is the operation's to record, since only it knows whether it is a read.
/// </summary>
public sealed class AccessDecisionService(
    IPermissionService permissions,
    IEnumerable<IAccessCeiling> ceilings,
    IEnumerable<IResourcePermissionMapper> mappers,
    IAccessAuditor auditor) : IAccessDecision
{
    private const int MaxDepth = 4;

    public async Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object? resource = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrEmpty(permission);

        var decision = await DecideCoreAsync(caller, permission, resource, 0, cancellationToken);
        if (!decision.IsAllowed)
        {
            await auditor.RecordAsync(new AccessEvent(
                AccessEventKind.Decision,
                permission,
                caller,
                decision.Verdict,
                Resource: resource?.ToString(),
                Reason: decision.Reason));
        }

        return decision;
    }

    private async Task<AccessDecision> DecideCoreAsync(CallerContext caller, string permission, object? resource, int depth, CancellationToken cancellationToken)
    {
        foreach (var ceiling in ceilings)
        {
            if (ceiling.Deny(caller, permission, resource) is { } reason)
            {
                return AccessDecision.Ceilinged(reason);
            }
        }

        if (caller.IsSuperUser)
        {
            return AccessDecision.Allowed;
        }

        IReadOnlyList<PermissionCandidate> candidates = [permission];
        if (resource is not null)
        {
            foreach (var mapper in mappers)
            {
                if (await mapper.MapAsync(permission, resource, caller, cancellationToken) is { } mapped)
                {
                    candidates = mapped;
                    break;
                }
            }
        }

        foreach (var candidate in candidates)
        {
            if (candidate.Permission == PermissionCandidate.GrantedPermission)
            {
                return AccessDecision.Allowed;
            }

            if (candidate.Resource is not null)
            {
                if (depth >= MaxDepth)
                {
                    continue;
                }

                var nested = await DecideCoreAsync(caller, candidate.Permission, candidate.Resource, depth + 1, cancellationToken);
                if (nested.IsFinal)
                {
                    return nested;
                }

                if (nested.IsAllowed)
                {
                    return AccessDecision.Allowed;
                }

                continue;
            }

            if (await IsGrantedAsync(caller, candidate.Permission))
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
