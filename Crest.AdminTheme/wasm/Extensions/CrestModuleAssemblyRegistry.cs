using System.Reflection;
using Crest.Components.Modules;

namespace Crest.AdminTheme;

/// <summary>
/// The admin module client libraries currently loaded: the assemblies the admin router
/// routes, page-region discovery runs over, and JS components are registered from.
/// </summary>
/// <remarks>
/// Crest never references a host's modules, so the set comes from the host app at runtime:
/// - browser: the module list the host's WASM build generated (Crest.LazyModules), handed over
///   by <see cref="CrestAdminClientServiceCollectionExtensions.AddCrestLazyModules"/> before
///   anything reads it. Lazy modules are not loaded yet and are skipped; the router adds
///   them as they load;
/// - server: the module client libraries in the app's output directory - *.BlazorWasm, the
///   same convention Crest.Server's Startup uses to feed module pages to the route table.
/// Admin only: a library declaring another shell (<see cref="CrestShellAttribute"/>) is left
/// out, or its pages would become routable in the admin shell.
/// </remarks>
internal static class CrestModuleAssemblyRegistry
{
    private static IReadOnlyList<Assembly>? _assemblies;

    public static IReadOnlyList<Assembly> Assemblies => _assemblies ??= Load(OutputDirectoryModules());

    public static void Configure(IEnumerable<string> moduleNames) => _assemblies = Load(moduleNames);

    private static IReadOnlyList<Assembly> Load(IEnumerable<string> moduleNames)
    {
        var assemblies = new List<Assembly>();
        foreach (var name in moduleNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Assembly assembly;
            try
            {
                assembly = Assembly.Load(name);
            }
            catch
            {
                continue;
            }

            var shell = assembly.GetCustomAttribute<CrestShellAttribute>()?.Shell ?? CrestShells.Admin;
            if (shell == CrestShells.Admin)
            {
                assemblies.Add(assembly);
            }
        }

        return assemblies;
    }

    private static IEnumerable<string> OutputDirectoryModules()
    {
        try
        {
            return Directory.EnumerateFiles(AppContext.BaseDirectory, "*.BlazorWasm.dll", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return [];
        }
    }
}
