using System.Xml.Linq;
using Grove.Sitemaps.Models;

namespace Grove.Sitemaps.Builders;

/// <summary>
/// Builds a sitemap.
/// </summary>
public interface ISitemapBuilder
{
    Task<XDocument> BuildAsync(SitemapType sitemap, SitemapBuilderContext context);
}
