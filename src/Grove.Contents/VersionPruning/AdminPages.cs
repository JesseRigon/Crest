using Grove.Contents.VersionPruning.Drivers;
using Grove.Routing;

namespace Grove.Contents.VersionPruning;

/// <summary>
/// The content version pruning settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("content-version-pruning", "Content Version Pruning", "settings", ContentVersionPruningSettingsDisplayDriver.GroupId, [ContentVersionPruningPermissions.ManageContentVersionPruningSettings.Name]);
    }
}
