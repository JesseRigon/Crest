using Grove.Routing;

namespace Grove.Microsoft.Authentication;

/// <summary>
/// The Microsoft Entra ID settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class EntraIdPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("microsoftentraid", "Microsoft Entra ID", "settings-authentication", MicrosoftAuthenticationConstants.Features.AAD, [Permissions.ManageMicrosoftAuthentication.Name]);
    }
}
