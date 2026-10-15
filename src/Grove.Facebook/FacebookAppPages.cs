using Grove.Routing;

namespace Grove.Facebook;

/// <summary>
/// The Meta app settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class FacebookAppPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("facebookApp", "Meta App", "settings-integrations", FacebookConstants.Features.Core, [Permissions.ManageFacebookApp.Name]);
    }
}
