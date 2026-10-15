using Grove.Routing;

namespace Grove.Google;

/// <summary>
/// The Google Tag Manager settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class GoogleTagManagerPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("googleTagManager", "Google Tag Manager", "settings-integrations", GoogleConstants.Features.GoogleTagManager, [Permissions.ManageGoogleTagManager.Name]);
    }
}
