using Grove.ContentManagement.Display.ContentDisplay;
using Grove.ContentManagement.Display.Models;
using Grove.DisplayManagement.Views;
using Grove.Lists.Models;

namespace Grove.Lists.Feeds;

/// <summary>
/// The list's RSS link in the page head: the <c>ListPartFeed</c> shape carries the link's values
/// (the feed address, or the proxy address the list names), and the <c>HeadLink</c> component
/// bound to it contributes the <c>&lt;link&gt;</c>. Nothing when the list disables its feed. The
/// feed settings (<c>DisableRssFeed</c>, <c>FeedProxyUrl</c>, <c>FeedItemsCount</c>) are the
/// part's own values, edited in the native editor's list section.
/// </summary>
public sealed class ListPartFeedDisplayDriver : ContentPartDisplayDriver<ListPart>
{
    public override IDisplayResult Display(ListPart listPart, BuildPartDisplayContext context)
    {
        bool disabled = listPart.Content.DisableRssFeed ?? false;
        if (disabled)
        {
            return null;
        }

        string proxy = listPart.Content.FeedProxyUrl;
        return Dynamic("ListPartFeed", shape =>
        {
            shape.ContentItem = listPart.ContentItem;
            shape.Rel = "alternate";
            shape.Type = "application/rss+xml";
            shape.Title = listPart.ContentItem.DisplayText;
            shape.Href = string.IsNullOrWhiteSpace(proxy)
                ? $"Contents/Lists/{Uri.EscapeDataString(listPart.ContentItem.ContentItemId)}/rss"
                : proxy;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content");
    }
}
