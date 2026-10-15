using Microsoft.AspNetCore.Http;

namespace Grove.Data.Feeds.Models;

public class FeedContext
{
    public FeedContext(HttpContext httpContext, string format)
    {
        HttpContext = httpContext;
        Format = format;
        Response = new FeedResponse();
    }

    /// <summary>The feed request.</summary>
    public HttpContext HttpContext { get; }

    public string Format { get; set; }
    public FeedResponse Response { get; set; }
    public IFeedBuilder Builder { get; set; }

    /// <summary>A value of the feed request: a route value of the feed's path, else a query value.</summary>
    public string GetValue(string name)
    {
        if (HttpContext.Request.RouteValues.TryGetValue(name, out var routeValue) && routeValue is not null)
        {
            return routeValue.ToString();
        }

        var query = HttpContext.Request.Query[name];
        return query.Count > 0 ? query[0] : null;
    }
}
