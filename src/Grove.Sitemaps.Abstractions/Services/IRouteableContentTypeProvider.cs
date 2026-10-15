using Grove.ContentManagement;
using Grove.ContentManagement.Metadata.Models;
using Grove.Sitemaps.Builders;

namespace Grove.Sitemaps.Services;

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
