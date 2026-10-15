using Grove.Sitemaps.Models;

namespace Grove.Sitemaps.Builders;

/// <summary>
/// Provides a last modified date for a sitemap.
/// </summary>
public interface ISitemapModifiedDateProvider
{
    Task<DateTime?> GetLastModifiedDateAsync(SitemapType sitemap);
}
