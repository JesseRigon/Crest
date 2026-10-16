using Grove.Contents.Management.Display.ContentDisplay;
using Grove.Contents.Management.Display.Models;
using Grove.Display.Management.Views;
using Grove.Contents.Hierarchy.Models;

namespace Grove.Contents.Hierarchy.Feeds;

/// <summary>
/// The list's RSS link in the page head: the <c>ChildContentItemsFieldSetFeed</c> shape carries the link's values
/// (the feed address, or the proxy address the list names), and the <c>HeadLink</c> component
/// bound to it contributes the <c>&lt;link&gt;</c>. Nothing when the list disables its feed. The
/// feed settings (<c>DisableRssFeed</c>, <c>FeedProxyUrl</c>, <c>FeedItemsCount</c>) are the
/// part's own values, edited in the native editor's list section.
/// </summary>
public sealed class ChildContentItemsFieldSetFeedDisplayDriver : ContentFieldSetDisplayDriver<ChildContentItemsFieldSet>
{
    public override IDisplayResult Display(ChildContentItemsFieldSet childContentItemsFieldSet, BuildFieldSetDisplayContext context)
    {
        var disabled = childContentItemsFieldSet.DisableRssFeed;
        if (disabled)
        {
            return null;
        }

        var proxy = childContentItemsFieldSet.FeedProxyUrl;
        return Dynamic("ChildContentItemsFieldSetFeed", shape =>
        {
            shape.ContentItem = childContentItemsFieldSet.ContentItem;
            shape.Rel = "alternate";
            shape.Type = "application/rss+xml";
            shape.Title = childContentItemsFieldSet.ContentItem.DisplayText;
            shape.Href = string.IsNullOrWhiteSpace(proxy)
                ? $"Contents/Lists/{Uri.EscapeDataString(childContentItemsFieldSet.ContentItem.ContentItemId)}/rss"
                : proxy;
        })
        .Location(PlatformConstants.DisplayType.Detail, "Content");
    }
}
