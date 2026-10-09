using Crest.Sitemaps.Models;

namespace Crest.Sitemaps.Builders;

/// <summary>
/// Provides a last modified date for a sitemap.
/// </summary>
public interface ISitemapModifiedDateProvider
{
    Task<DateTime?> GetLastModifiedDateAsync(SitemapType sitemap);
}
