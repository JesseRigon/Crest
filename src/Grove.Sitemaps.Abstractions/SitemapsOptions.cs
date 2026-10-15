using Microsoft.AspNetCore.Routing;

namespace Grove.Sitemaps;

public class SitemapsOptions
{
    public RouteValueDictionary GlobalRouteValues { get; set; } = [];
    public string SitemapIdKey { get; set; } = "";
}
