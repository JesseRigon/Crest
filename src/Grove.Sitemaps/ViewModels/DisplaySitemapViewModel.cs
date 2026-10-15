using Grove.Sitemaps.Models;

namespace Grove.Sitemaps.ViewModels;

public class DisplaySitemapViewModel
{
    public SitemapType Sitemap { get; set; }
    public IEnumerable<dynamic> Items { get; set; }
    public IDictionary<string, dynamic> Thumbnails { get; set; }
}
