using System.Xml.Linq;
using Grove.Feeds.Models;

namespace Grove.Feeds;

public interface IFeedBuilder
{
    Task<XDocument> ProcessAsync(FeedContext context, Func<Task> populate);

    FeedItem<TItem> AddItem<TItem>(FeedContext context, TItem contentItem);

    void AddProperty(FeedContext context, FeedItem feedItem, XElement element);
}
