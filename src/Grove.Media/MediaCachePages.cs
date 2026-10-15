using Grove.Routing;

namespace Grove.Media;

/// <summary>
/// The media cache: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class MediaCachePages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("media-cache", "Cache", "settings-media", "Grove.Media", "MediaCache", "Index", [MediaPermissions.ManageAssetCache.Name]);
    }
}
