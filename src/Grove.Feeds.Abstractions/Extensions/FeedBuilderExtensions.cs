using System.Xml.Linq;
using Grove.Feeds.Models;

namespace Grove.Feeds;

public static class FeedBuilderExtensions
{
    public static void AddProperty(this IFeedBuilder feedBuilder, FeedContext context, FeedItem feedItem, string name, string value)
        => feedBuilder.AddProperty(context, feedItem, new XElement(name, value));
}
