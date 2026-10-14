using Crest.Routing;

namespace Crest.Shortcodes;

/// <summary>
/// Shortcode templates: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("shortcodes", "Shortcodes", "design", "Crest.Shortcodes", "Admin", "Index", [ShortcodesPermissions.ManageShortcodeTemplates.Name], icon: "@iconify:mdi:code-braces");
    }
}
