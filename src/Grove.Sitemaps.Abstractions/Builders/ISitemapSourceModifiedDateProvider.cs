using Grove.Sitemaps.Models;

namespace Grove.Sitemaps.Builders;

/// <summary>
/// Provides a last modified date from a sitemap source.
/// </summary>
public interface ISitemapSourceModifiedDateProvider
{
    Task<DateTime?> GetLastModifiedDateAsync(SitemapSource source);
}
