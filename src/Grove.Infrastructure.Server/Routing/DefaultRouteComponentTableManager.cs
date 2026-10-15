namespace Grove.Routing;

/// <summary>
/// The route table, a view over the page registry: one entry per route of every page entry that
/// has a component, declared pages first, then the dynamic ones (designed pages). The registry
/// caches the declared layer for the shell's lifetime and keys it by the active theme pair.
/// </summary>
public sealed class DefaultRouteComponentTableManager(IPageRegistry registry) : IRouteComponentTableManager
{
    private RouteComponentTable? _table;

    public async Task<RouteComponentTable> GetRouteComponentTableAsync()
    {
        if (_table is not null)
        {
            return _table;
        }

        var pages = await registry.GetAsync();
        return _table = new RouteComponentTable([.. pages.Entries
            .Where(entry => entry.Component is not null)
            .SelectMany(entry => RoutesOf(entry, pages).Select(route => new RouteComponentEntry(
                route,
                entry.Component!,
                entry.Shell,
                IsDefaultLanding: entry.IsDefaultLanding,
                AllowsAnonymous: entry.AllowsAnonymous,
                IsOrganizationFree: entry.IsOrganizationFree,
                ClientOnly: entry.ClientOnly)))]);
    }

    // A page whose routes a source declares ([PageEntry(DeclaredBySource = true)]: a kind
    // registry's generic page, the settings form) serves only those concrete routes, so a path
    // its template would also match (a settings group still served by a legacy page) is not
    // taken for it.
    private static IEnumerable<string> RoutesOf(PageEntry entry, PageSet pages) =>
        !entry.DeclaredBySource
            ? entry.Routes
            : pages.Entries
                .Where(declared => declared.Component is null && declared.Shell == entry.Shell)
                .SelectMany(declared => declared.Routes)
                .Where(route => !route.Contains('{') && entry.Routes.Any(template => RouteTemplates.Matches(template, route)))
                .Distinct(StringComparer.OrdinalIgnoreCase);
}
