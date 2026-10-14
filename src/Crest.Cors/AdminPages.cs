using Crest.Routing;

namespace Crest.Cors;

/// <summary>
/// CORS settings: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("cors", "Cross-Origin Resource Sharing", "settings-security", "Crest.Cors", "Admin", "Index", [Permissions.ManageCorsSettings.Name]);
    }
}
