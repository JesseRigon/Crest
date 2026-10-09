using Crest.Feeds.Models;

namespace Crest.Feeds;

public interface IFeedQueryProvider
{
    Task<FeedQueryMatch> MatchAsync(FeedContext context);
}

public class FeedQueryMatch
{
    public int Priority { get; set; }
    public IFeedQuery FeedQuery { get; set; }
}
