using Grove.Data.Documents;

namespace Grove.Sitemaps.Models;

public class SitemapDocument : Document
{
    public IDictionary<string, SitemapType> Sitemaps { get; set; } = new Dictionary<string, SitemapType>();
}
