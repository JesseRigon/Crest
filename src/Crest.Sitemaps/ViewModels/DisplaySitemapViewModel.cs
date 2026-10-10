using Crest.Sitemaps.Models;

namespace Crest.Sitemaps.ViewModels;

public class DisplaySitemapViewModel
{
    public SitemapType Sitemap { get; set; }
    public IEnumerable<dynamic> Items { get; set; }
    public IDictionary<string, dynamic> Thumbnails { get; set; }
}
