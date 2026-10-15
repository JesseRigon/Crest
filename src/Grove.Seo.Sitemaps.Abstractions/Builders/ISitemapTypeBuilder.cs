using Grove.Seo.Sitemaps.Models;

namespace Grove.Seo.Sitemaps.Builders;

/// <summary>
/// Builds a sitemap source.
/// </summary>
public interface ISitemapTypeBuilder
{
    Task BuildAsync(SitemapType sitemap, SitemapBuilderContext context);
}
