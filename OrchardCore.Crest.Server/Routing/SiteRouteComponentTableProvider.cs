namespace Crest.Routing;

// The site-bucket counterpart of MemberRouteComponentTableProvider: the site theme's own
// client assembly plus every module client library declaring
// [assembly: CrestShell(CrestShells.Site)] (a module's site-wasm/ library). The bucket is
// read from the assembly (ShellAssemblies), never from the page, because the client router
// can only be scoped by assembly - see CrestShellAttribute.
public sealed class SiteRouteComponentTableProvider : IRouteComponentTableProvider
{
    public RouteBucket Bucket => RouteBucket.Site;

    public IEnumerable<RouteComponentEntry> GetRouteComponents() =>
        ShellAssemblies.InBucket(Bucket).SelectMany(assembly =>
            AssemblyRouteComponentScanner.Scan(assembly, Bucket, defaultLandingRoutePattern: "/"));
}
