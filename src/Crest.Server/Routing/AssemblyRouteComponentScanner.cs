using System.Reflection;
using Crest.Components.Modules;
using Crest.DisplayManagement.Components;

namespace Crest.Routing;

// Shared scan step behind every IRouteComponentTableProvider: the module pages (docs/blazor-
// display.md § 6a) of an assembly, read from the same catalog as its components (rework step
// 1), each [Route] @page compiles to. This is the registration point itself (per agents.md's
// "no invented path literals" rule) - nobody re-types a route string anywhere else; every other
// piece of code that needs to know a route consults the resulting RouteComponentEntry, not a
// second, hand-typed constant.
public static class AssemblyRouteComponentScanner
{
    public static IEnumerable<RouteComponentEntry> Scan(Assembly assembly, ShellId shell, string? defaultLandingRoutePattern = null) =>
        ComponentCatalogs.ForAssembly(assembly).DescribePages().Select(page => new RouteComponentEntry(
            page.Template,
            page.Type,
            shell,
            IsDefaultLanding: defaultLandingRoutePattern is not null
                && string.Equals(page.Template, defaultLandingRoutePattern, StringComparison.OrdinalIgnoreCase),
            AllowsAnonymous: page.AllowsAnonymous,
            IsOrganizationFree: page.Type.IsDefined(typeof(OrganizationFreePageAttribute), inherit: false)));
}
