using System.Reflection;
using Crest.Components.Modules;

namespace Crest.Routing;

/// <summary>
/// Which shell a client assembly's pages belong to, read from its
/// <see cref="CrestShellAttribute"/>.
/// </summary>
/// <remarks>
/// The one place the server answers "which shell is this assembly", so the route-table
/// providers and the endpoint shell stamping in Startup cannot disagree: a page in one
/// shell for the table and another for the gate is reachable in neither.
/// </remarks>
public static class ShellAssemblies
{
    /// <summary>
    /// The shell of a Crest client assembly, or null for an assembly that is not one
    /// (framework, server, domain libraries).
    /// </summary>
    public static ShellId? GetShell(Assembly assembly)
    {
        var declared = assembly.GetCustomAttribute<CrestShellAttribute>()?.Shell;
        if (!string.IsNullOrWhiteSpace(declared))
        {
            return new ShellId(declared);
        }

        // A module client library that declares nothing is admin: that is where module
        // pages have always gone, and it keeps every existing module working unchanged.
        return ModuleClientAssemblies.IsModuleClient(assembly.GetName().Name)
            ? ShellId.Admin
            : null;
    }

    /// <summary>Every loaded client assembly whose pages belong to <paramref name="shell"/>.</summary>
    public static IEnumerable<Assembly> InShell(ShellId shell) =>
        AppDomain.CurrentDomain.GetAssemblies().Where(assembly => GetShell(assembly) == shell);
}
