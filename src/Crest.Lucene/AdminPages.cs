using Crest.Routing;

namespace Crest.Lucene;

/// <summary>
/// The Lucene query runner: Legacy (MVC) admin pages, in the menu until they are ported (docs/admin-menu.md › The page registry).
/// </summary>
public sealed class AdminPages : IPageSource
{
    public IEnumerable<PageEntry> GetPages()
    {
        yield return PageEntries.Legacy("lucenequery", "Run Lucene Query", "queries", "Crest.Lucene", "Admin", "Query", [LuceneSearchPermissions.ManageLuceneIndexes.Name]);
    }
}
