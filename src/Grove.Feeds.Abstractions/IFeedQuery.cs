using Grove.Feeds.Models;

namespace Grove.Feeds;

public interface IFeedQuery
{
    Task ExecuteAsync(FeedContext context);
}
