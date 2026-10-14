using Crest.Routing;

namespace Crest.Microsoft.Authentication;

/// <summary>
/// The Microsoft account settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class MicrosoftAccountPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("microsoft", "Microsoft", "settings-authentication", MicrosoftAuthenticationConstants.Features.MicrosoftAccount, [Permissions.ManageMicrosoftAuthentication.Name]);
    }
}
