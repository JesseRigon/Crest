using Crest.Feeds.Models;

namespace Crest.Feeds;

public interface IFeedQuery
{
    Task ExecuteAsync(FeedContext context);
}
