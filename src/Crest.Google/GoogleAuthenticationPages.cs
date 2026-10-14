using Crest.Routing;

namespace Crest.Google;

/// <summary>
/// The Google authentication settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class GoogleAuthenticationPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("google", "Google", "settings-authentication", GoogleConstants.Features.GoogleAuthentication, [Permissions.ManageGoogleAuthentication.Name]);
    }
}
