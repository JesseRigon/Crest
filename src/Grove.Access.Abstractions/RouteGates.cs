namespace Grove.Access;

/// <summary>A route the caller may open, as the client's navigation receives it.</summary>
public sealed record RouteAccess(string Template);

/// <summary>
/// The page gate (docs/access.md: the access gate owns the pipeline, pages included). Each page
/// declares its permissions once in the page registry (docs/admin-menu.md › The page registry);
/// the gate and the admin menu both ask this one service, decided by the one decision for the
/// caller the request's gate built (never from the principal). A path no page admits is denied.
/// </summary>
public interface IRouteAuthorization
{
    /// <summary>The admin routes the caller may open.</summary>
    Task<RouteAccess[]> GetAuthorizedRoutesAsync(CallerContext? caller, CancellationToken cancellationToken = default);

    /// <summary>Whether the caller may open the admin page at the shell-relative <paramref name="path"/>.</summary>
    Task<bool> CanAccessAsync(CallerContext? caller, string? path, CancellationToken cancellationToken = default);

    /// <summary>Whether the caller holds any one of a page's <paramref name="permissions"/>: what the menu asks for an item, the same question the gate asks for its route.</summary>
    Task<bool> AllowsAnyAsync(CallerContext? caller, IReadOnlyList<string> permissions, CancellationToken cancellationToken = default);
}
