using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Seo.Sitemaps.Builders;

namespace Grove.Seo.Sitemaps.Services;

/// <summary>
/// Provides routable content types to the coordinator.
/// </summary>
public interface IRouteableContentTypeProvider
{
    /// <summary>
    /// Provides routable content types.
    /// </summary>
    Task<IEnumerable<ContentTypeDefinition>> ListRoutableTypeDefinitionsAsync();

    /// <summary>
    /// Gets the route for a content item, when building a sitemap.
    /// </summary>
    Task<string> GetRouteAsync(SitemapBuilderContext context, ContentItem contentItem);
}
