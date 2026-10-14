using Crest.Routing;

namespace Crest.Sitemaps;

/// <summary>
/// Sitemaps: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        string[] permissions = [SitemapsPermissions.ManageSitemaps.Name];
        yield return PageEntries.Group("sitemaps-seo", "Search Engine Optimization", "tools");
        yield return PageEntries.Legacy("sitemaps", "Sitemaps", "sitemaps-seo", "Crest.Sitemaps", "Admin", "List", permissions, "1");
        yield return PageEntries.Legacy("sitemap-indexes", "Sitemap Indexes", "sitemaps-seo", "Crest.Sitemaps", "SitemapIndex", "List", permissions, "2");
        yield return PageEntries.Legacy("sitemaps-cache", "Sitemaps Cache", "sitemaps-seo", "Crest.Sitemaps", "SitemapCache", "List", permissions, "3");
    }
}
