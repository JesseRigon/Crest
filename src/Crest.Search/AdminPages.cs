using Crest.Routing;

namespace Crest.Search;

/// <summary>
/// The site search settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.LegacySettings("searchsettings", "Site Search", "settings-search", SearchConstants.SearchSettingsGroupId, [SearchPermissions.ManageSearchSettings.Name]);
    }
}
