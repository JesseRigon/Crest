using Grove.Https.Drivers;
using Grove.Routing;

namespace Grove.Https;

/// <summary>
/// The HTTPS settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("https", "HTTPS", "settings-security", HttpsSettingsDisplayDriver.GroupId, [Permissions.ManageHttps.Name]);
    }
}
