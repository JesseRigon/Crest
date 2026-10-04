using System.Reflection;
using Crest.Components.Modules;

namespace Crest.Routing;

/// <summary>
/// Which shell a client assembly's pages belong to, read from its
/// <see cref="CrestShellAttribute"/>.
/// </summary>
/// <remarks>
/// The one place the server answers "which bucket is this assembly", so the route-table
/// providers and the endpoint bucket stamping in Startup cannot disagree: a page in one
/// bucket for the table and another for the gate is reachable in neither.
/// </remarks>
public static class ShellAssemblies
{
    private const string ModuleClientSuffix = ".BlazorWasm";

    /// <summary>
    /// The bucket of a Crest client assembly, or null for an assembly that is not one
    /// (framework, server, domain libraries).
    /// </summary>
    public static RouteBucket? GetBucket(Assembly assembly)
    {
        var declared = assembly.GetCustomAttribute<CrestShellAttribute>()?.Shell;
        if (declared is not null)
        {
            return declared switch
            {
                CrestShells.Admin => RouteBucket.Admin,
                CrestShells.Site => RouteBucket.Site,
                CrestShells.Member => RouteBucket.Member,
                _ => null,
            };
        }

        // A module client library that declares nothing is admin: that is where module
        // pages have always gone, and it keeps every existing module working unchanged.
        return assembly.GetName().Name?.EndsWith(ModuleClientSuffix, StringComparison.Ordinal) == true
            ? RouteBucket.Admin
            : null;
    }

    /// <summary>Every loaded client assembly whose pages belong to <paramref name="bucket"/>.</summary>
    public static IEnumerable<Assembly> InBucket(RouteBucket bucket) =>
        AppDomain.CurrentDomain.GetAssemblies().Where(assembly => GetBucket(assembly) == bucket);
}
