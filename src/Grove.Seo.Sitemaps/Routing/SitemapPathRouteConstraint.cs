using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Seo.Sitemaps.Routing;

/// <summary>
/// <c>{**sitemap:sitemapPath}</c>: matches a path only when it is the path of an enabled sitemap of
/// the tenant, so the sitemap endpoint owns those paths and no other (the shell's route ownership
/// evaluates the same constraint). Link generation is the address scheme's job: outbound, it always
/// matches.
/// </summary>
public sealed class SitemapPathRouteConstraint : IRouteConstraint
{
    public const string Name = "sitemapPath";

    public bool Match(HttpContext httpContext, IRouter route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (routeDirection != RouteDirection.IncomingRequest)
        {
            return true;
        }

        if (httpContext is null || !values.TryGetValue(routeKey, out var value) || value?.ToString() is not { Length: > 0 } path)
        {
            return false;
        }

        var entries = httpContext.RequestServices.GetService<SitemapEntries>();
        if (entries is null)
        {
            return false;
        }

        // Route constraints are synchronous; the entries are an in-memory map rebuilt on change.
        var (found, _) = entries.TryGetSitemapIdByPathAsync(path).GetAwaiter().GetResult();
        return found;
    }
}
