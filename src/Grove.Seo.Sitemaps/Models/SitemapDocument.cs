using Grove.Data.Documents;

namespace Grove.Seo.Sitemaps.Models;

public class SitemapDocument : Document
{
    public IDictionary<string, SitemapType> Sitemaps { get; set; } = new Dictionary<string, SitemapType>();
}
