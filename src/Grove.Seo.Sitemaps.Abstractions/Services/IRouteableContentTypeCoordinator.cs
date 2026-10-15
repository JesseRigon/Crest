using Grove.Contents.Management;
using Grove.Contents.Management.Metadata.Models;
using Grove.Seo.Sitemaps.Builders;

namespace Grove.Seo.Sitemaps.Services;

/// <summary>
/// Coordinates content types that are routable.
/// </summary>
public interface IRouteableContentTypeCoordinator
{
    /// <summary>
    /// Lists all routable content types.
    /// </summary>
    Task<IEnumerable<ContentTypeDefinition>> ListRoutableTypeDefinitionsAsync();

    /// <summary>
    /// Gets the route for a content item, when building a sitemap.
    /// </summary>
    Task<string> GetRouteAsync(SitemapBuilderContext context, ContentItem contentItem);
}
