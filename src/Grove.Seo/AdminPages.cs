using Grove.Routing;

namespace Grove.Seo;

/// <summary>
/// The SEO settings (robots): Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Group("seo", "Search Engine Optimization", "settings-search");
        yield return PageEntries.LegacySettings("seo-robots", "Robots", "seo", SeoConstants.RobotsSettingsGroupId, [SeoConstants.ManageSeoSettings.Name]);
    }
}
