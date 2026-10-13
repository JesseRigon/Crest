namespace Crest.Access.Services;

/// <summary>
/// The page gate over every module's <see cref="IRoutePermissionProvider"/>: the one decision
/// for each route's permission, for the request's caller. A batch projection for the client's
/// navigation and the shell selector's check before a page renders; the services behind a page
/// stay the authority for every data operation.
/// </summary>
public sealed class RouteAuthorizationService(IAccessDecision decision, IEnumerable<IRoutePermissionProvider> providers) : IRouteAuthorization
{
    public async Task<RouteAccess[]> GetAuthorizedRoutesAsync(CallerContext? caller, CancellationToken cancellationToken = default)
    {
        var granted = new List<RouteAccess>();
        if (caller is null)
        {
            return [];
        }

        foreach (var route in GetRoutes())
        {
            if ((await decision.DecideAsync(caller, route.Permission, null, cancellationToken)).IsAllowed)
            {
                granted.Add(new RouteAccess(route.Template));
            }
        }

        return granted.ToArray();
    }

    public async Task<bool> CanAccessAsync(CallerContext? caller, string? path, CancellationToken cancellationToken = default)
    {
        var route = GetRoutes().FirstOrDefault(candidate => RouteTemplates.Matches(candidate.Template, path));
        return route is not null && caller is not null && (await decision.DecideAsync(caller, route.Permission, null, cancellationToken)).IsAllowed;
    }

    private RoutePermission[] GetRoutes() => providers
        .SelectMany(provider => provider.GetRoutes())
        .GroupBy(route => route.Template, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToArray();
}
