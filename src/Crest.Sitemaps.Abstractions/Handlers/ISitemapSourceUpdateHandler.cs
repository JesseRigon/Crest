using Crest.Sitemaps.Models;

namespace Crest.Sitemaps.Handlers;

/// <summary>
/// Handles sitemaps updates based on their <see cref="SitemapSource"/> of a given type.
/// </summary>
public interface ISitemapSourceUpdateHandler
{
    Task UpdateSitemapAsync(SitemapUpdateContext context);
}
