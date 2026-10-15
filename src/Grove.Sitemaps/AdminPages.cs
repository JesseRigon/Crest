using Grove.Routing;

namespace Grove.Sitemaps;

/// <summary>
/// Sitemaps: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        string[] permissions = [SitemapsPermissions.ManageSitemaps.Name];
        yield return PageEntries.Group("sitemaps-seo", "Search Engine Optimization", "tools");
        yield return PageEntries.Legacy("sitemaps", "Sitemaps", "sitemaps-seo", "Grove.Sitemaps", "Admin", "List", permissions, "1");
        yield return PageEntries.Legacy("sitemap-indexes", "Sitemap Indexes", "sitemaps-seo", "Grove.Sitemaps", "SitemapIndex", "List", permissions, "2");
        yield return PageEntries.Legacy("sitemaps-cache", "Sitemaps Cache", "sitemaps-seo", "Grove.Sitemaps", "SitemapCache", "List", permissions, "3");
    }
}
