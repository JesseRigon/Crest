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

            var module = assembly.GetName().Name;
            var zoneKeys = assembly.GetCustomAttributes<ZoneKeyAttribute>()
                .Select(key => new ZoneKeyDescriptor(key.Key, module, key.Allowed, key.Instances, key.RequiredOnLayout, key.DisplayName))
                .ToArray();

            return new FixedCatalog([.. catalog.Describe()], zoneKeys, [.. ContributionsOf(assembly, module)], [.. PagesOf(assembly)]);
        });

    // The code form of a zone contribution: the module library's page-region contributors
    // (docs/page-regions.md), each created once with its parameterless constructor.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Contributors are kept with their assembly: the shells load module libraries untrimmed, as the page-region scan always has.")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "As above.")]
    private static IEnumerable<ZoneContribution> ContributionsOf(Assembly assembly, string? module)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = [.. exception.Types.OfType<Type>()];
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || !typeof(Crest.Components.Regions.IPageRegionContributor).IsAssignableFrom(type) || type.GetConstructor(Type.EmptyTypes) is null)
            {
                continue;
            }

            if (Activator.CreateInstance(type) is Crest.Components.Regions.IPageRegionContributor contributor)
            {
                yield return new ZoneContribution(contributor.RegionKey, contributor.ComponentType, contributor.Order, module);
            }
        }
    }

    // The routes @page compiles to, read off the exported component types.
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Routed components are kept by the router; the scan reads only their attributes.")]
    private static IEnumerable<PageDescriptor> PagesOf(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetExportedTypes();
        }
        catch (Exception exception) when (exception is ReflectionTypeLoadException or NotSupportedException)
        {
            yield break;
        }

        foreach (var type in types)
        {
            if (!typeof(Microsoft.AspNetCore.Components.IComponent).IsAssignableFrom(type))
            {
                continue;
            }

            var anonymous = type.GetCustomAttributes(inherit: false).Any(attribute => attribute.GetType().FullName == "Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute");
            var entries = type.GetCustomAttributes<PageEntryAttribute>(inherit: false).ToArray();
            foreach (var route in type.GetCustomAttributes<Microsoft.AspNetCore.Components.RouteAttribute>(inherit: false))
            {
                // The declaration naming this route, else the one that names none.
                var entry = entries.FirstOrDefault(candidate => string.Equals(candidate.Route, route.Template, StringComparison.OrdinalIgnoreCase))
                    ?? entries.FirstOrDefault(candidate => candidate.Route is null);
                yield return new PageDescriptor(type, route.Template, anonymous, entry);
            }
        }
    }

    private sealed class FixedCatalog(IReadOnlyList<ComponentDescriptor> descriptors, IReadOnlyList<ZoneKeyDescriptor> zoneKeys, IReadOnlyList<ZoneContribution> contributions, IReadOnlyList<PageDescriptor> pages) : IComponentCatalog
    {
        public IEnumerable<PageDescriptor> DescribePages() => pages;

        public IEnumerable<ComponentDescriptor> Describe() => descriptors;

        public IEnumerable<ZoneKeyDescriptor> DescribeZoneKeys() => zoneKeys;

        public IEnumerable<ZoneContribution> DescribeContributions() => contributions;
    }
}
