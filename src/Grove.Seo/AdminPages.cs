using Grove.Routing;

namespace Grove.Seo;

/// <summary>
/// The SEO settings section of the menu; its Robots page is a settings group (Startup).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Group("seo", "Search Engine Optimization", "settings-search");
    }
}
