using Crest.Data.Documents;

namespace Crest.Sitemaps.Models;

public class SitemapDocument : Document
{
    public IDictionary<string, SitemapType> Sitemaps { get; set; } = new Dictionary<string, SitemapType>();
}
