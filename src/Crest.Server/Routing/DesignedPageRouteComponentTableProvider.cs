using Crest.Templates.Models;
using Crest.Templates.Services;

namespace Crest.Routing;

// Designed pages (docs/blazor-display.md decision 9, § 6a): each published page record is an
// entry of its own bucket, so it is reachable only through that bucket's shell. A site page
// renders through the site's content route component (its default landing), which asks
// SitePageBuilder for the path. The admin and member shells have no component that renders a
// data-defined page yet, so their pages are not registered.
public sealed class DesignedPageRouteComponentTableProvider(DesignedPageStore pages) : IDataRouteComponentTableProvider
{
    public async Task<IEnumerable<RouteComponentEntry>> GetRouteComponentsAsync(RouteComponentTable scanned)
    {
        if (scanned.DefaultLanding(ShellId.Site) is not { } site)
        {
            return [];
        }

        return (await pages.ListPublishedAsync())
            .Where(page => page.Bucket == DesignedPagesConstants.SiteBucket)
            .Select(page => new RouteComponentEntry(page.Route, site.ComponentType, ShellId.Site, AllowsAnonymous: page.AllowAnonymous))
            .ToArray();
    }
}
