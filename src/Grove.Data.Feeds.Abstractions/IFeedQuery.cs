using Grove.Data.Feeds.Models;

namespace Grove.Data.Feeds;

public interface IFeedQuery
{
    Task ExecuteAsync(FeedContext context);
}
