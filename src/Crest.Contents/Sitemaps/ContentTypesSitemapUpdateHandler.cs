using Crest.ContentManagement.Handlers;
using Crest.Sitemaps.Handlers;

namespace Crest.Contents.Sitemaps;

public class ContentTypesSitemapUpdateHandler : ContentHandlerBase
{
    private readonly ISitemapUpdateHandler _sitemapUpdateHandler;

    public ContentTypesSitemapUpdateHandler(ISitemapUpdateHandler sitemapUpdateHandler)
    {
        _sitemapUpdateHandler = sitemapUpdateHandler;
    }

    public override Task PublishedAsync(PublishContentContext context) => UpdateSitemapAsync(context);

    public override Task UnpublishedAsync(PublishContentContext context) => UpdateSitemapAsync(context);

    private Task UpdateSitemapAsync(ContentContextBase context)
    {
        var updateContext = new SitemapUpdateContext
        {
            UpdateObject = context.ContentItem,
        };

        return _sitemapUpdateHandler.UpdateSitemapAsync(updateContext);
    }
}
