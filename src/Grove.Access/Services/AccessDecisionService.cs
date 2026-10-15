using Grove.Environment.Extensions.Features;
using Microsoft.Extensions.Logging;
using Grove.Security.Permissions;

namespace Grove.Access.Services;

/// <summary>
/// The one decision: ceilings first (a single deny wins), then the super user, then the
/// resource mapping (the authority that claims the resource, by feature priority, else the
/// module's own mapper; a candidate may re-ask the question for another resource), then the
/// caller's permissions with the implied-by chain resolved. Every denial is recorded; an
/// allow is the operation's to record, since only it knows whether it is a read.
/// </summary>
public sealed class AccessDecisionService(
    IPermissionService permissions,
    IEnumerable<IAccessCeiling> ceilings,
    IEnumerable<IResourcePermissionMapper> mappers,
    IAccessAuditor auditor,
    IProviderPriority? priority = null,
    ILogger<AccessDecisionService>? logger = null) : IAccessDecision
{
    private const int MaxDepth = 4;

    // The two mapper roles, each in feature-priority order (highest first): authorities grouped
    // by priority, so two claimants at one priority are seen as the conflict they are. There is
    // no additive role: a mapper that could grant past the authority would be the authority;
    // a domain that wants contributions takes them inside its authority (sharing's entry
    // sources), and narrowing is a ceiling.
    private readonly IResourcePermissionMapper[][] _authorities = [.. mappers
        .OfType<IAuthoritativeResourcePermissionMapper>()
        .GroupBy(mapper => PriorityOf(priority, mapper))
        .OrderByDescending(group => group.Key)
        .Select(group => group.Cast<IResourcePermissionMapper>().ToArray())];

    private readonly IResourcePermissionMapper[] _modules = [.. mappers
        .Where(mapper => mapper is not IAuthoritativeResourcePermissionMapper)
        .Select((mapper, index) => (mapper, index))
        .OrderByDescending(entry => PriorityOf(priority, entry.mapper))
        .ThenBy(entry => entry.index)
        .Select(entry => entry.mapper)];

    private static int PriorityOf(IProviderPriority? priority, IResourcePermissionMapper mapper) => priority?.PriorityOf(mapper.GetType()) ?? 0;

    public Task<AccessDecision> DecideAsync(CallerContext caller, string permission, object? resource = null, CancellationToken cancellationToken = default) =>
        DecideAsync(caller, permission, instance: null, resource, cancellationToken);

    /// <summary>
    /// The same, for a permission instance in hand (a requirement, a dynamic per-type
    /// permission built at run time): its implied-by chain counts first, then the registered
    /// permission's of the same name, so an instance the registry does not hold keeps its chain.
    /// </summary>
    public Task<AccessDecision> DecideAsync(CallerContext caller, Permission permission, object? resource = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(permission);
        return DecideAsync(caller, permission.Name, permission, resource, cancellationToken);
    }

    private async Task<AccessDecision> DecideAsync(CallerContext caller, string permission, Permission? instance, object? resource, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentException.ThrowIfNullOrEmpty(permission);

        var decision = await DecideCoreAsync(caller, permission, instance, resource, 0, cancellationToken);
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

    private async Task<AccessDecision> DecideCoreAsync(CallerContext caller, string permission, Permission? instance, object? resource, int depth, CancellationToken cancellationToken)
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
            // The super user passes every grant and a mapping conflict, never a ceiling: a
            // candidate that re-asks for another resource (an attachment's item) is still asked,
            // so a ceiling met there stays final.
            if (resource is not null && depth < MaxDepth)
            {
                var reasked = await MapAsync(permission, resource, caller, cancellationToken);
                foreach (var candidate in reasked.Candidates ?? [])
                {
                    if (candidate.Resource is not null
                        && await DecideCoreAsync(caller, candidate.Permission, null, candidate.Resource, depth + 1, cancellationToken) is { IsFinal: true } nested)
                    {
                        return nested;
                    }
                }
            }

            return AccessDecision.Allowed;
        }

        IReadOnlyList<PermissionCandidate> candidates = [permission];
        if (resource is not null)
        {
            var mapped = await MapAsync(permission, resource, caller, cancellationToken);
            if (mapped.Conflict is { } conflict)
            {
                return AccessDecision.Ceilinged(conflict);
            }

            candidates = mapped.Candidates ?? candidates;
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

                var nested = await DecideCoreAsync(caller, candidate.Permission, null, candidate.Resource, depth + 1, cancellationToken);
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

            if (await IsGrantedAsync(caller, candidate.Permission, string.Equals(candidate.Permission, instance?.Name, StringComparison.OrdinalIgnoreCase) ? instance : null))
            {
                return AccessDecision.Allowed;
            }
        }

        return AccessDecision.Denied($"'{permission}' is not granted.");
    }

    /// <summary>
    /// The candidates for a resource-bound request: the authority that claims it (highest
    /// priority; two at one priority conflict), else the first module mapper that answers, else
    /// none (the plain permission).
    /// </summary>
    private async Task<(IReadOnlyList<PermissionCandidate>? Candidates, string? Conflict)> MapAsync(string permission, object resource, CallerContext caller, CancellationToken cancellationToken)
    {
        IReadOnlyList<PermissionCandidate>? candidates = null;
        foreach (var group in _authorities)
        {
            IResourcePermissionMapper? claimant = null;
            foreach (var mapper in group)
            {
                if (await mapper.MapAsync(permission, resource, caller, cancellationToken) is not { } answer)
                {
                    continue;
                }

                if (claimant is not null)
                {
                    var reason = $"'{permission}' on {resource} is claimed by two authorities at the same priority ({claimant.GetType().Name}, {mapper.GetType().Name}); give one feature a higher priority.";
                    logger?.LogError("Access configuration conflict: {Reason}", reason);
                    return (null, reason);
                }

                claimant = mapper;
                candidates = answer;
            }

            if (claimant is not null)
            {
                break;
            }
        }

        if (candidates is null)
        {
            foreach (var mapper in _modules)
            {
                if (await mapper.MapAsync(permission, resource, caller, cancellationToken) is { } answer)
                {
                    candidates = answer;
                    break;
                }
            }
        }

        return (candidates, null);
    }

    private async Task<bool> IsGrantedAsync(CallerContext caller, string permissionName, Permission? instance)
    {
        if (caller.Permissions.Contains(permissionName))
        {
            return true;
        }

        // The instance's chain first, then the registered permission's: one resolution of
        // implied-by for every surface (docs/access.md, Overlaps to remove).
        var granting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (instance is not null)
        {
            Collect(instance, granting);
            if (granting.Overlaps(caller.Permissions))
            {
                return true;
            }
        }

        var registered = await permissions.FindByNameAsync(permissionName);
        if (registered is null)
        {
            return false;
        }

        granting.Clear();
        Collect(registered, granting);
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
