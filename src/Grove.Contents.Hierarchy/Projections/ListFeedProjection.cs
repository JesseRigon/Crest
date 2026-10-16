using Grove.Contents.Management.Handlers;
using Grove.Contents.Hierarchy.Models;
using Grove.Data.Feeds.Models;

namespace Grove.Contents.Hierarchy.Projections;

/// <summary>A list's feed metadata (a read projection): whether its RSS feed is offered, and its proxy address.</summary>
public sealed class ListFeedProjection : ContentFieldSetProjection<ChildContentItemsFieldSet>
{
    protected override Task ProjectAsync(ContentItemAspectContext context, ChildContentItemsFieldSet part)
    {
        return context.ForAsync<FeedMetadata>(feedMetadata =>
        {
            feedMetadata.DisableRssFeed = part.DisableRssFeed;
            feedMetadata.FeedProxyUrl = part.FeedProxyUrl;

            return Task.CompletedTask;
        });
    }
}
