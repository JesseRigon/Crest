using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using Crest.Sitemaps.Builders;
using Crest.Sitemaps.Models;
using Crest.Sitemaps.Services;
using YesSql;
using YesSql.Services;

namespace Crest.Contents.Sitemaps;

public class ContentTypesSitemapSourceModifiedDateProvider : SitemapSourceModifiedDateProviderBase<ContentTypesSitemapSource>
{
    private readonly IRouteableContentTypeCoordinator _routeableContentTypeCoordinator;
    private readonly ISession _session;

    public ContentTypesSitemapSourceModifiedDateProvider(
        IRouteableContentTypeCoordinator routeableContentTypeCoordinator,
        ISession session
        )
    {
        _routeableContentTypeCoordinator = routeableContentTypeCoordinator;
        _session = session;
    }

    public override async Task<DateTime?> GetLastModifiedDateAsync(ContentTypesSitemapSource source)
    {
        ContentItem lastModifiedContentItem;

        if (source.IndexAll)
        {
            var typesToIndex = (await _routeableContentTypeCoordinator.ListRoutableTypeDefinitionsAsync())
                .Select(ctd => ctd.Name);

            var query = _session.Query<ContentItem>()
                .With<ContentItemIndex>(x => x.Published && x.ContentType.IsIn(typesToIndex))
                .OrderByDescending(x => x.ModifiedUtc);

            lastModifiedContentItem = await query.FirstOrDefaultAsync();
        }
        else if (source.LimitItems)
        {
            var query = _session.Query<ContentItem>()
                .With<ContentItemIndex>(x => x.Published && x.ContentType == source.LimitedContentType.ContentTypeName)
                .OrderByDescending(x => x.ModifiedUtc);

            lastModifiedContentItem = await query.FirstOrDefaultAsync();
        }
        else
        {
            var typesToIndex = (await _routeableContentTypeCoordinator.ListRoutableTypeDefinitionsAsync())
                .Where(ctd => source.ContentTypes.Any(s => string.Equals(ctd.Name, s.ContentTypeName, StringComparison.Ordinal)))
                .Select(ctd => ctd.Name);

            // This is an estimate, so doesn't take into account Take/Skip values.
            var query = _session.Query<ContentItem>()
                .With<ContentItemIndex>(x => x.Published && x.ContentType.IsIn(typesToIndex))
                .OrderByDescending(x => x.ModifiedUtc);

            lastModifiedContentItem = await query.FirstOrDefaultAsync();
        }

        return lastModifiedContentItem.ModifiedUtc;
    }
}
