using Grove.Routing;

namespace Grove.UrlRewriting;

/// <summary>
/// URL rewriting rules: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("urlRewriting", "URL Rewriting", "tools", "Grove.UrlRewriting", "Admin", "Index", [UrlRewritingPermissions.ManageUrlRewritingRules.Name]);
    }
}
