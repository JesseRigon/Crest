using System.Xml.Linq;
using Grove.Contents.Localization.Models;
using Grove.Contents.Management;
using Grove.Seo.Sitemaps.Aspects;
using Grove.Seo.Sitemaps.Builders;
using Grove.Seo.Sitemaps.Services;

namespace Grove.Contents.Localization.Sitemaps;

public class SitemapUrlHrefLangExtendedMetadataProvider : ISitemapContentItemExtendedMetadataProvider
{
    private static readonly XNamespace s_extendedNamespace = "http://www.w3.org/1999/xhtml";
    private static readonly XAttribute s_extendedAttribute = new(XNamespace.Xmlns + "xhtml", s_extendedNamespace);

    private readonly IContentManager _contentManager;
    private readonly IRouteableContentTypeCoordinator _routeableContentTypeCoordinator;

    public SitemapUrlHrefLangExtendedMetadataProvider(
        IContentManager contentManager,
        IRouteableContentTypeCoordinator routeableContentTypeCoordinator
        )
    {
        _contentManager = contentManager;
        _routeableContentTypeCoordinator = routeableContentTypeCoordinator;
    }

    public XAttribute GetExtendedAttribute => s_extendedAttribute;

    public async Task<bool> ApplyExtendedMetadataAsync(
        SitemapBuilderContext context,
        ContentItemsQueryContext queryContext,
        ContentItem contentItem,
        XElement url)
    {
        if (!contentItem.TryGet<LocalizationFieldSet>(out var part) ||
            queryContext.ReferenceContentItems == null ||
            !queryContext.ReferenceContentItems.Any())
        {
            return true;
        }

        var localizedContentFieldSets = queryContext.ReferenceContentItems
            .Select(ci => ci.TryGet<LocalizationFieldSet>(out var localizationFieldSet) ? localizationFieldSet : null)
            .Where(cp => cp?.LocalizationSet == part.LocalizationSet);

        foreach (var localizedPart in localizedContentFieldSets)
        {
            var sitemapMetadataAspect = await _contentManager.PopulateAspectAsync<SitemapMetadataAspect>(localizedPart.ContentItem);
            if (sitemapMetadataAspect.Exclude)
            {
                continue;
            }

            var hrefValue = await _routeableContentTypeCoordinator.GetRouteAsync(context, localizedPart.ContentItem);

            var linkNode = new XElement(s_extendedNamespace + "link",
                new XAttribute("rel", "alternate"),
                new XAttribute("hreflang", localizedPart.Culture),
                new XAttribute("href", hrefValue));

            url.Add(linkNode);
        }

        return true;
    }
}
