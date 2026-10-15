using Grove.Seo.Sitemaps.Models;

namespace Grove.Seo.Sitemaps.Handlers;

/// <summary>
/// Handles sitemaps updates based on their <see cref="SitemapSource"/>.
/// </summary>
public interface ISitemapTypeUpdateHandler
{
    Task UpdateSitemapAsync(SitemapUpdateContext context);
}
