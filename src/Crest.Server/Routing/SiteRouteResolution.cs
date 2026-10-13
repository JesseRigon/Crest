using Microsoft.AspNetCore.Http;

namespace Crest.Routing;

/// <summary>How the site shell serves a page request (the fallback shell, no base of its own).</summary>
public enum SiteRouteKind
{
    /// <summary>A module's site page: the request path is its literal, served as is.</summary>
    ModulePage,

    /// <summary>The content page ("/"): a designed page or a content path, rewritten to "/".</summary>
    ContentPage,

    /// <summary>A legacy (MVC) endpoint owns the path: passed through untouched.</summary>
    Legacy,
}

public sealed record SiteRouteResolution(SiteRouteKind Kind, PathString Path);

/// <summary>
/// Resolves a site page request through the site route table, the way the admin and member
/// shells resolve theirs - one routing system (ruling 2026-10-13).
/// </summary>
public static class SiteRouteResolver
{
    /// <summary>
    /// The site's answer for <paramref name="path"/>: a module site page wins; otherwise a legacy
    /// endpoint that owns the path (<paramref name="legacyOwned"/>, asked only then) is forwarded
    /// to; otherwise the content page, which also renders designed pages (their table entries
    /// name the content page's component, whose one literal is "/").
    /// </summary>
    public static SiteRouteResolution Resolve(RouteComponentTable table, PathString path, Func<bool> legacyOwned)
    {
        var contentPage = table.DefaultLanding(ShellId.Site)?.ComponentType;
        if (table.TryMatch(path, ShellId.Site, out var entry) && entry.ComponentType != contentPage)
        {
            return new SiteRouteResolution(SiteRouteKind.ModulePage, path);
        }

        return legacyOwned()
            ? new SiteRouteResolution(SiteRouteKind.Legacy, path)
            : new SiteRouteResolution(SiteRouteKind.ContentPage, new PathString("/"));
    }
}
