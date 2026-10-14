using Crest.Admin;
using Crest.Routing;

namespace Crest.Demo;

/// <summary>
/// The demo module's section: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Group("demo", "Demo", position: "10");
        yield return PageEntries.Legacy("demo-index", "Demo Admin", "demo", "Crest.Demo", "Admin", "Index", [AdminPermissions.AccessAdminPanel.Name], "0");
        yield return PageEntries.Legacy("demo-todo", "Todo (Liquid - Frontend)", "demo", "Crest.Demo", "Todo", "Index", [AdminPermissions.AccessAdminPanel.Name], "1");
    }
}
