using Grove.Feeds.Models;

namespace Grove.Feeds;

public interface IFeedItemBuilder
{
    Task PopulateAsync(FeedContext context);
}
