using Crest.Feeds.Models;

namespace Crest.Feeds;

public interface IFeedItemBuilder
{
    Task PopulateAsync(FeedContext context);
}
