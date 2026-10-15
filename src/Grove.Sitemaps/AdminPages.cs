using Grove.Routing;

namespace Grove.Sitemaps;

/// <summary>
/// The Search Engine Optimization group under Tools: the sitemaps pages (Grove.Sitemaps.BlazorWasm)
/// declare themselves under it.
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Group("sitemaps-seo", "Search Engine Optimization", "tools");
    }
}
