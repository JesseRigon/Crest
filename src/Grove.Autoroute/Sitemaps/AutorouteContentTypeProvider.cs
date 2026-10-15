using Microsoft.AspNetCore.Mvc;
using Grove.Autoroute.Models;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Models;
using Grove.Sitemaps.Builders;
using Grove.Sitemaps.Services;

namespace Grove.Autoroute.Sitemaps;

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

            // UrlHelper.Action includes BasePath automatically if present.
            // If content item is assigned as home route, UrlHelper resolves as site root.
            return context.HostPrefix + context.UrlHelper.Action(routes["Action"].ToString(), routes);
        }

        return null;
    }

    public async Task<IEnumerable<ContentTypeDefinition>> ListRoutableTypeDefinitionsAsync()
    {
        return (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(ctd => ctd.Parts.Any(p => p.Name == nameof(AutoroutePart)));
    }
}
