namespace Grove.Data.Feeds;

/// <summary>
/// The paths feeds are served at (anonymous <c>GET</c>, mapped by the Feeds module): a module
/// declares the path of its feed and the format it answers in, e.g.
/// <c>options.MapFeed("Contents/Lists/{contentItemId}/rss", "rss")</c>. The path's route values
/// and the query reach the feed queries through <see cref="Models.FeedContext.GetValue"/>.
/// </summary>
public class FeedRoutesOptions
{
    private readonly List<FeedRoute> _routes = [];

    public IReadOnlyList<FeedRoute> Routes => _routes;

    public FeedRoutesOptions MapFeed(string pattern, string format)
    {
        _routes.Add(new FeedRoute(pattern, format));
        return this;
    }
}

public sealed record FeedRoute(string Pattern, string Format);
