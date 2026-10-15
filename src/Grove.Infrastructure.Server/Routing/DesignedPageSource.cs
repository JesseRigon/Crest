using Grove.Display.Templates.Models;
using Grove.Display.Templates.Services;

namespace Grove.Routing;

// Designed pages (docs/blazor-display.md decision 9, § 6a): the page registry's defined entries.
// Each published page record is an entry of its own bucket, so it is reachable only through that
// bucket's shell. A site page renders through the site's content route component (its default
// landing), which asks SitePageBuilder for the path. The admin and member shells have no
// component that renders a data-defined page yet, so their pages are not registered. The store
// caches itself and is invalidated on publish.
public sealed class DesignedPageSource(DesignedPageStore pages) : IDynamicPageSource
{
    public async Task<IEnumerable<PageEntry>> GetPagesAsync(PageSet declared, CancellationToken cancellationToken = default)
    {
        if (declared.InShell(ShellId.Site).FirstOrDefault(entry => entry.IsDefaultLanding && entry.Component is not null) is not { } site)
        {
            return [];
        }

        return (await pages.ListPublishedAsync())
            .Where(page => page.Bucket == DesignedPagesConstants.SiteBucket)
            .Select(page => new PageEntry
            {
                Id = "designed-page:" + page.Route,
                Shell = ShellId.Site,
                Routes = [page.Route],
                Component = site.Component,
                AllowsAnonymous = page.AllowAnonymous,
                DeclaredBy = typeof(DesignedPageSource).FullName,
            })
            .ToArray();
    }
}
