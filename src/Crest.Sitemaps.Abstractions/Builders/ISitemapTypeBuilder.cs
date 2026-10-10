using Crest.Sitemaps.Models;

namespace Crest.Sitemaps.Builders;

/// <summary>
/// Builds a sitemap source.
/// </summary>
public interface ISitemapTypeBuilder
{
    Task BuildAsync(SitemapType sitemap, SitemapBuilderContext context);
}
