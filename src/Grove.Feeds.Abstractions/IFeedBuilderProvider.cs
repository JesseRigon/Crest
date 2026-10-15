using Grove.Feeds.Models;

namespace Grove.Feeds;

public interface IFeedBuilderProvider
{
    FeedBuilderMatch Match(FeedContext context);
}

public class FeedBuilderMatch
{
    public int Priority { get; set; }
    public IFeedBuilder FeedBuilder { get; set; }
}
