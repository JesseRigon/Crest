using System.Xml.Linq;
using Grove.Routing;

namespace Grove.Seo.Sitemaps.Builders;

public class SitemapBuilderContext
{
    public string HostPrefix { get; set; }
    /// <summary>The tenant's link generator for the sitemap request (paths include the tenant's path base).</summary>
    public IShellLinkGenerator Links { get; set; }
    public SitemapResponse Response { get; set; }
}

public class SitemapResponse
{
    public XElement ResponseElement { get; set; }
}
