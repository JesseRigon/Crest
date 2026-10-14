using Crest.Routing;

namespace Crest.Facebook;

/// <summary>
/// The Meta Pixel settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class FacebookPixelPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("facebookPixel", "Meta Pixel", "settings-integrations", FacebookConstants.PixelSettingsGroupId, [FacebookConstants.ManageFacebookPixelPermission.Name]);
    }
}
