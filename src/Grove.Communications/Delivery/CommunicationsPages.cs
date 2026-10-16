using Grove.Communications.Delivery;
using Grove.Routing;

namespace Grove.Communications.Delivery;

/// <summary>
/// The Communications section of the admin menu: the overview page, each channel's settings and
/// test send, and the delivery log declare themselves under it.
/// </summary>
public sealed class CommunicationsPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages() =>
    [
        PageEntries.Group(CommunicationsConstants.MenuSection, "Communications", position: "after.20", icon: "@iconify:mdi:message-text-outline"),
    ];
}
