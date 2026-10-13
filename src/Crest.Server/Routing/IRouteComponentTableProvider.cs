namespace Crest.Routing;

// One implementation per Blazor shell (Admin, Site, Member, ...). Each is the sole authority
// on its own routes - nobody hand-maintains a central route list anywhere else. Mirrors
// IShapeTableProvider's role for shapes: this is the "scan my own assemblies by convention"
// contract, not a registration list.
public interface IRouteComponentTableProvider
{
    ShellId Shell { get; }

    IEnumerable<RouteComponentEntry> GetRouteComponents();
}

/// <summary>
/// Route entries defined by data (docs/blazor-display.md decision 9, designed pages): read per
/// request from a source that caches itself and is invalidated on publish, and added to the
/// shell's table after the scanned module pages, under the same shell gate and authorization.
/// </summary>
public interface IDataRouteComponentTableProvider
{
    Task<IEnumerable<RouteComponentEntry>> GetRouteComponentsAsync(RouteComponentTable scanned);
}
