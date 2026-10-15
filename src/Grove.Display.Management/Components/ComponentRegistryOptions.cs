#nullable enable

using System.Reflection;

namespace Grove.Display.Management.Components;

/// <summary>
/// The component assemblies a tenant's registry reads besides its enabled features' own
/// assemblies: shared libraries (Grove.Display.Components, Grove.Display.Icons), theme clients and module
/// client libraries. Which feature owns each is worked out when the registry is built (§ 1).
/// </summary>
public sealed class ComponentRegistryOptions
{
    private readonly Dictionary<Assembly, ShellKind?> _assemblies = [];

    /// <summary>The assemblies added, each with the one shell it is limited to, if any.</summary>
    public IReadOnlyDictionary<Assembly, ShellKind?> Assemblies => _assemblies;

    /// <summary>
    /// Adds an assembly's components. <paramref name="shell"/> limits them to one shell: a module
    /// client library belongs to the shell whose router loads it, whatever its components declare.
    /// </summary>
    public ComponentRegistryOptions Add(Assembly assembly, ShellKind? shell = null)
    {
        _assemblies[assembly] = shell;
        return this;
    }
}
