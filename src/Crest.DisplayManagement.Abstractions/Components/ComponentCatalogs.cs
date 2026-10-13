using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Crest.DisplayManagement.Components;

/// <summary>Finds an assembly's catalog: the generated one it names, or the reflection fallback.</summary>
public static class ComponentCatalogs
{
    private static readonly ConcurrentDictionary<Assembly, IComponentCatalog> s_catalogs = new();

    /// <summary>
    /// The assembly's <see cref="IComponentCatalog"/>: the one its
    /// <c>[assembly: ComponentCatalog]</c> names (generated, trim-safe), otherwise a
    /// <see cref="ReflectionComponentCatalog"/>. Descriptors are read once per assembly.
    /// </summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The reflection catalog only runs for assemblies built without the generator, which are not trimmed.")]
    public static IComponentCatalog ForAssembly(Assembly assembly) =>
        s_catalogs.GetOrAdd(assembly, static assembly =>
        {
            IComponentCatalog catalog = assembly.GetCustomAttribute<ComponentCatalogAttribute>() is { } declared
                ? (IComponentCatalog)Activator.CreateInstance(declared.CatalogType)!
                : new ReflectionComponentCatalog(assembly);

            return new FixedCatalog([.. catalog.Describe()]);
        });

    private sealed class FixedCatalog(IReadOnlyList<ComponentDescriptor> descriptors) : IComponentCatalog
    {
        public IEnumerable<ComponentDescriptor> Describe() => descriptors;
    }
}
