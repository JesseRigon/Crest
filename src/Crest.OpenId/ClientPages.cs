using Crest.Routing;

namespace Crest.OpenId;

/// <summary>
/// The OpenID Connect client settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class ClientPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("openid-client", "Authentication Client", "openid-settings", "Crest.OpenId.Client", [OpenIdPermissions.ManageClientSettings.Name]);
    }
}
