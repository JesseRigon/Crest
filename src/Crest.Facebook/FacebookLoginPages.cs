using Crest.Routing;

namespace Crest.Facebook;

/// <summary>
/// The Meta login settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class FacebookLoginPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("facebook", "Meta", "settings-authentication", FacebookConstants.Features.Login, [Permissions.ManageFacebookApp.Name]);
    }
}
