namespace Crest.Routing;

// Scans Admin's own client assembly, plus every module client library in the admin bucket
// (e.g. Accounting.BlazorWasm), for @page-attributed components - the same set Startup.cs's
// ThemeOwnerMetadata stamping uses, both decided by ShellAssemblies, so a module's pages are
// reachable through the gate instead of only rendering. A module's member library declares
// [assembly: CrestShell(CrestShells.Member)] and is MemberRouteComponentTableProvider's.
// Bucket is the fixed RouteBucket.Admin value, not a theme-id lookup - see
// ThemeOwnerMetadata's comment for why a raw theme-id comparison is the wrong question.
public sealed class AdminRouteComponentTableProvider : IRouteComponentTableProvider
{
    public RouteBucket Bucket => RouteBucket.Admin;

    public IEnumerable<RouteComponentEntry> GetRouteComponents() =>
        ShellAssemblies.InBucket(Bucket).SelectMany(assembly =>
            AssemblyRouteComponentScanner.Scan(assembly, Bucket, defaultLandingRoutePattern: "/Dashboard"));
}
