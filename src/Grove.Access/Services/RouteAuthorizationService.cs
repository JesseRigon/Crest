using Grove.Routing;

namespace Grove.Access.Services;

/// <summary>
/// The page gate over the page registry: each admin route admits a caller holding any one of the
/// permissions its entry declares, decided by the one decision for the request's caller. A batch
/// projection for the client's navigation, the shell selector's check before a page renders and
/// the admin menu's check per item; the services behind a page stay the authority for every data
/// operation.
/// </summary>
public sealed class RouteAuthorizationService(IAccessDecision decision, IPageRegistry registry) : IRouteAuthorization
{
    public async Task<RouteAccess[]> GetAuthorizedRoutesAsync(CallerContext? caller, CancellationToken cancellationToken = default)
    {
        if (caller is null)
        {
            return [];
        }

        var pages = await registry.GetAsync(cancellationToken);
        var granted = new List<RouteAccess>();
        var decided = new Dictionary<PageEntry, bool>(ReferenceEqualityComparer.Instance);
        foreach (var (entry, template) in pages.Routes(ShellId.Admin))
        {
            if (!decided.TryGetValue(entry, out var allowed))
            {
                decided[entry] = allowed = await AllowsAnyAsync(caller, entry.Permissions, cancellationToken);
            }

            if (allowed)
            {
                granted.Add(new RouteAccess(template));
            }
        }

        return [.. granted];
    }

    public async Task<bool> CanAccessAsync(CallerContext? caller, string? path, CancellationToken cancellationToken = default)
    {
        if (caller is null)
        {
            return false;
        }

        var entry = (await registry.GetAsync(cancellationToken)).MatchRoute(ShellId.Admin, path);
        return entry is not null && await AllowsAnyAsync(caller, entry.Permissions, cancellationToken);
    }

    public async Task<bool> AllowsAnyAsync(CallerContext? caller, IReadOnlyList<string> permissions, CancellationToken cancellationToken = default)
    {
        if (caller is null)
        {
            return false;
        }

        foreach (var permission in permissions)
        {
            if ((await decision.DecideAsync(caller, permission, null, cancellationToken)).IsAllowed)
            {
                return true;
            }
        }

        return false;
    }
}
