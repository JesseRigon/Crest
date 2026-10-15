using Grove.Routing;

namespace Grove.Media;

/// <summary>
/// The media API settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// The library, profiles and options are native pages.
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("media-api", "API", "settings-media", MediaApiSettings.GroupId, [MediaPermissions.ManageMediaApiSettings.Name]);
    }
}
