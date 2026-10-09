using System.Xml.Linq;
using Crest.Sitemaps.Models;

namespace Crest.Sitemaps.Builders;

/// <summary>
/// Builds a sitemap.
/// </summary>
public interface ISitemapBuilder
{
    Task<XDocument> BuildAsync(SitemapType sitemap, SitemapBuilderContext context);
}
