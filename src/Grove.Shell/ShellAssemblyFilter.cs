using System.Reflection;
using Grove.Components.Modules;

namespace Grove.Shell;

/// <summary>
/// Which shell a loaded client assembly belongs to, browser side - the counterpart of the
/// server's <c>Grove.Routing.ShellAssemblies</c>, reading the same
/// <see cref="GroveShellAttribute"/>.
/// </summary>
/// <remarks>
/// Each shell's router is handed only assemblies this says belong to it. That is the client
/// half of UI isolation: Blazor's Router routes every [Route] component in the assemblies
/// it is given, so handing the member router an admin library would make that library's
/// pages reachable inside the member shell on client-side navigation.
/// </remarks>
public static class ShellAssemblyFilter
{
    /// <summary>
    /// The shell an assembly declares; a module library declaring none is admin, where
    /// module pages have always gone.
    /// </summary>
    public static string ShellOf(Assembly assembly) =>
        assembly.GetCustomAttribute<GroveShellAttribute>()?.Shell ?? GroveShells.Admin;

    public static bool BelongsTo(Assembly assembly, string shell) =>
        string.Equals(ShellOf(assembly), shell, StringComparison.Ordinal);
}
