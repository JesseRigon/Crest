using System.Xml.Linq;
using Grove.Seo.Sitemaps.Models;

namespace Grove.Seo.Sitemaps.Builders;

/// <summary>
/// Builds a sitemap.
/// </summary>
public interface ISitemapBuilder
{
    Task<XDocument> BuildAsync(SitemapType sitemap, SitemapBuilderContext context);
}
