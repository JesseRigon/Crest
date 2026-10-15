using System.Xml.Linq;
using Grove.Contents.Management;
using Grove.Contents.Management.Models;
using Grove.Contents.Management.Records;
using Grove.Data.Feeds;
using Grove.Data.Feeds.Models;
using Grove.Display.Lists.Indexes;
using YesSql;

namespace Grove.Display.Lists.Feeds;

public class ListFeedQuery : IFeedQueryProvider, IFeedQuery
{
    public const int DefaultItemsCount = 20;

    private readonly IContentManager _contentManager;
    private readonly ISession _session;

    public ListFeedQuery(IContentManager contentManager, ISession session)
    {
        _contentManager = contentManager;
        _session = session;
    }

    public async Task<FeedQueryMatch> MatchAsync(FeedContext context)
    {
        var contentItemId = context.GetValue("contentItemId");
        if (contentItemId == null)
        {
            return null;
        }

        var contentItem = await _contentManager.GetAsync(contentItemId);
        if (contentItem == null)
        {
            return null;
        }

        var feedMetadata = await _contentManager.PopulateAspectAsync<FeedMetadata>(contentItem);

        if (feedMetadata.DisableRssFeed)
        {
            return null;
        }

        return new FeedQueryMatch { FeedQuery = this, Priority = -5 };
    }

    public async Task ExecuteAsync(FeedContext context)
    {
        var contentItemId = context.GetValue("contentItemId");
        if (contentItemId == null)
        {
            return;
        }

        var contentItem = await _contentManager.GetAsync(contentItemId);

        if (contentItem == null)
        {
            return;
        }

        var contentItemMetadata = await _contentManager.PopulateAspectAsync<ContentItemMetadata>(contentItem);
        var bodyAspect = await _contentManager.PopulateAspectAsync<BodyAspect>(contentItem);
        var routes = contentItemMetadata.DisplayRouteValues;

        if (context.Format == "rss")
        {
            var link = new XElement("link");
            context.Response.Element.SetElementValue("title", contentItem.DisplayText);
            context.Response.Element.Add(link);

            context.Response.Element.Add(new XElement("description", new XCData(bodyAspect.Body?.ToString() ?? string.Empty)));

            context.Response.Contextualize(contextualize =>
            {
                var url = contextualize.Links.GetUriByRouteValues(routes);

                link.Add(url);
            });
        }
        else
        {
            context.Builder.AddProperty(context, null, "title", contentItem.DisplayText);
            context.Builder.AddProperty(context, null, new XElement("description", new XCData(bodyAspect.Body?.ToString() ?? string.Empty)));

            context.Response.Contextualize(contextualize =>
            {
                var url = contextualize.Links.GetUriByRouteValues(routes);

                context.Builder.AddProperty(context, null, "link", url);
            });
        }

        int itemsCount = contentItem.Content.ListPart?.FeedItemsCount ?? DefaultItemsCount;

        IEnumerable<ContentItem> items;

        items = itemsCount == 0
            ? Enumerable.Empty<ContentItem>()
            : await _session.Query<ContentItem>()
                .With<ContainedPartIndex>(x => x.ListContentItemId == contentItem.ContentItemId)
                .With<ContentItemIndex>(x => x.Published)
                .OrderByDescending(x => x.CreatedUtc)
                .Take(itemsCount)
                .ListAsync();

        foreach (var item in items)
        {
            context.Builder.AddItem(context, item);
        }
    }
}
