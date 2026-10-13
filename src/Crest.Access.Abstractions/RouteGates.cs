namespace Crest.Access;

/// <summary>
/// One page route and the permission that admits a caller to it (docs/access.md, ruling
/// 2026-10-11: the access gate owns the pipeline, pages included). The template is relative to
/// the shell (<c>/Features</c>, <c>/Contents/ContentItems/{ContentItemId}/Edit</c>); a
/// <c>{segment}</c> matches any one segment.
/// </summary>
public sealed record RoutePermission(string Template, string Permission);

/// <summary>A route the caller may open, as the client's navigation receives it.</summary>
public sealed record RouteAccess(string Template);

/// <summary>
/// Registered by each module for its own pages, so adding a domain never means editing the
/// gate: the route table is the union of every provider's routes, the first registration of a
/// template winning.
/// </summary>
public interface IRoutePermissionProvider
{
    IEnumerable<RoutePermission> GetRoutes();
}

/// <summary>
/// The page gate: which routes a caller may open, decided by the one decision for the caller
/// the request's gate built (never from the principal). A path no route matches is denied.
/// </summary>
public interface IRouteAuthorization
{
    Task<RouteAccess[]> GetAuthorizedRoutesAsync(CallerContext? caller, CancellationToken cancellationToken = default);

    Task<bool> CanAccessAsync(CallerContext? caller, string? path, CancellationToken cancellationToken = default);
}

public static class RouteTemplates
{
    /// <summary>Whether <paramref name="path"/> matches <paramref name="template"/>: segment by segment, ignoring case, a <c>{segment}</c> matching any one.</summary>
    public static bool Matches(string template, string? path)
    {
        var templateSegments = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = (path ?? string.Empty).Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (templateSegments.Length != pathSegments.Length)
        {
            return false;
        }

        for (var index = 0; index < templateSegments.Length; index++)
        {
            var segment = templateSegments[index];
            if (segment.StartsWith('{') && segment.EndsWith('}'))
            {
                continue;
            }

            if (!string.Equals(segment, pathSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
