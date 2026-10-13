namespace Crest.Routing;

// The member-bucket counterpart of AdminRouteComponentTableProvider: the member theme's own
// client assembly plus every module client library declaring
// [assembly: CrestShell(CrestShells.Member)]. The bucket is read from the assembly
// (ShellAssemblies), never from the page, because the client router can only be scoped by
// assembly - see CrestShellAttribute.
public sealed class MemberRouteComponentTableProvider : IRouteComponentTableProvider
{
    public ShellId Shell => ShellId.Member;

    public IEnumerable<RouteComponentEntry> GetRouteComponents() =>
        ShellAssemblies.InShell(Shell).SelectMany(assembly => AssemblyRouteComponentScanner.Scan(assembly, Shell));
}
