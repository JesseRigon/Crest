using System.Collections.Concurrent;
using System.Reflection;
using Crest.Components.Modules;

namespace Crest.Shell;

/// <summary>
/// The theme UI of each shell, as the loaded theme clients declare it
/// (<see cref="CrestShellAttribute.Routes"/>).
/// </summary>
public static class ShellThemes
{
    private static readonly ConcurrentDictionary<string, Type> s_routes = new(StringComparer.Ordinal);

    /// <summary>
    /// The Routes component of <paramref name="shell"/>'s theme client, or null when no loaded
    /// assembly declares one (the theme UI is not loaded yet, or the host ships none).
    /// </summary>
    public static Type? RoutesOf(string shell)
    {
        if (s_routes.TryGetValue(shell, out var cached))
        {
            return cached;
        }

        var routes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .Select(assembly => assembly.GetCustomAttribute<CrestShellAttribute>())
            .FirstOrDefault(attribute => attribute?.Routes is not null && string.Equals(attribute.Shell, shell, StringComparison.Ordinal))
            ?.Routes;

        return routes is null ? null : s_routes.GetOrAdd(shell, routes);
    }
}
