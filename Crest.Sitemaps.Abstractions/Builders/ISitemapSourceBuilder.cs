using Crest.Sitemaps.Models;

namespace Crest.Sitemaps.Builders;

/// <summary>
/// Builds items for a sitemap source.
/// </summary>
public interface ISitemapSourceBuilder
{
    Task BuildAsync(SitemapSource source, SitemapBuilderContext context);
}
