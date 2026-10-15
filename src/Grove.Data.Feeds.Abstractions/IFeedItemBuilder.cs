using Grove.Data.Feeds.Models;

namespace Grove.Data.Feeds;

public interface IFeedItemBuilder
{
    Task PopulateAsync(FeedContext context);
}
