using Grove.ReverseProxy.Drivers;
using Grove.Routing;

namespace Grove.ReverseProxy;

/// <summary>
/// The reverse proxy settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("reverseproxy", "Reverse Proxy", "settings", ReverseProxySettingsDisplayDriver.GroupId, [Permissions.ManageReverseProxySettings.Name]);
    }
}
