using Grove.Routes.Auto.Models;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Seo.Sitemaps.Builders;
using Grove.Seo.Sitemaps.Services;

namespace Grove.Routes.Auto.Sitemaps;

public class AutorouteContentTypeProvider : IRouteableContentTypeProvider
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IContentManager _contentManager;

    public AutorouteContentTypeProvider(
        IContentDefinitionManager contentDefinitionManager,
        IContentManager contentManager
        )
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentManager = contentManager;
    }

    public async Task<string> GetRouteAsync(SitemapBuilderContext context, ContentItem contentItem)
    {
        var ctd = (await ListRoutableTypeDefinitionsAsync())?.FirstOrDefault(ctd => ctd.Name == contentItem.ContentType);

        if (ctd != null)
        {
            var contentItemMetadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);
            var routes = contentItemMetadata.DisplayRouteValues;

            // The path includes the tenant's path base; the home route item resolves as the site root.
            return context.HostPrefix + context.Links.GetPathByRouteValues(routes);
        }

        return null;
    }

    public async Task<IEnumerable<ContentTypeDefinition>> ListRoutableTypeDefinitionsAsync()
    {
        return (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(ctd => ctd.Parts.Any(p => p.Name == nameof(AutoroutePart)));
    }
}
