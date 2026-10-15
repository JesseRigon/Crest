using Grove.Seo.Sitemaps.Models;

namespace Grove.Seo.Sitemaps.Builders;

/// <summary>
/// Provides a last modified date for a sitemap.
/// </summary>
public interface ISitemapModifiedDateProvider
{
    Task<DateTime?> GetLastModifiedDateAsync(SitemapType sitemap);
}
