using Grove.ContentManagement.Handlers;
using Grove.Feeds.Models;
using Grove.Lists.Models;

namespace Grove.Lists.Drivers;

public class ListPartFeedHandler : ContentPartHandler<ListPart>
{
    public override Task GetContentItemAspectAsync(ContentItemAspectContext context, ListPart part)
    {
        return context.ForAsync<FeedMetadata>(feedMetadata =>
        {
            // Enable the feed if the value is not defined. This is handy to migrate the old feeds
            feedMetadata.DisableRssFeed = part.Content.DisableRssFeed ?? false;
            feedMetadata.FeedProxyUrl = part.Content.FeedProxyUrl;

            return Task.CompletedTask;
        });
    }
}
