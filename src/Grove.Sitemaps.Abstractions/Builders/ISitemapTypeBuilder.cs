using Grove.Sitemaps.Models;

namespace Grove.Sitemaps.Builders;

/// <summary>
/// Builds a sitemap source.
/// </summary>
public interface ISitemapTypeBuilder
{
    Task BuildAsync(SitemapType sitemap, SitemapBuilderContext context);
}
