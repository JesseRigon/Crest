#nullable enable

using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Extensions;
using Crest.Environment.Extensions;
using Crest.Environment.Extensions.Features;
using Crest.Environment.Shell.Builders.Models;

namespace Crest.DisplayManagement.Components;

/// <summary>The tenant's component registries (§ 1), one per shell and theme.</summary>
public interface IComponentRegistryManager
{
    /// <summary>
    /// The registry for <paramref name="shell"/> under <paramref name="themeId"/>: the enabled
    /// features' components plus the shared libraries', the theme's and its base themes'. A
    /// null theme admits every enabled theme's components that serve the shell.
    /// </summary>
    IComponentRegistry GetRegistry(ShellKind shell, string? themeId = null);
}

/// <summary>
/// Builds a registry from the catalogs of the enabled features' assemblies and of the assemblies
/// in <see cref="ComponentRegistryOptions"/>, and caches it the way the shape table is cached:
/// a tenant singleton, so it is dropped with the shell when a feature or theme changes.
/// </summary>
/// <remarks>
/// A component belongs to the feature that ships its type (the shell blueprint maps every type
/// of an enabled feature's assembly to it); a component in a client library belongs to the module
/// the library is named after (<c>Fruitful.Accounting.BlazorWasm</c> is <c>Fruitful.Accounting</c>'s),
/// and one in a library no module owns (Crest.Components) to none. A component of a disabled
/// feature is absent. Catalogs are applied in feature order, so a later feature, and a theme
/// last, overrides an earlier one's name.
/// </remarks>
public sealed class ComponentRegistryManager : IComponentRegistryManager
{
    private readonly ConcurrentDictionary<(ShellKind Shell, string ThemeId), IComponentRegistry> _cache = new();
    private readonly ShellBlueprint _blueprint;
    private readonly IExtensionManager _extensionManager;
    private readonly ITypeFeatureProvider _typeFeatureProvider;
    private readonly ComponentRegistryOptions _options;

    public ComponentRegistryManager(
        ShellBlueprint blueprint,
        IExtensionManager extensionManager,
        ITypeFeatureProvider typeFeatureProvider,
        IOptions<ComponentRegistryOptions> options)
    {
        _blueprint = blueprint;
        _extensionManager = extensionManager;
        _typeFeatureProvider = typeFeatureProvider;
        _options = options.Value;
    }

    public IComponentRegistry GetRegistry(ShellKind shell, string? themeId = null) =>
        _cache.GetOrAdd((shell, themeId ?? string.Empty), static (key, manager) => manager.Build(key.Shell, key.ThemeId.Length == 0 ? null : key.ThemeId), this);

    private ComponentRegistry Build(ShellKind shell, string? themeId)
    {
        var enabled = _blueprint.Descriptor.Features.Select(feature => feature.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var order = _extensionManager.GetFeatures()
            .Select((feature, index) => (feature.Id, index))
            .ToDictionary(entry => entry.Id, entry => entry.index, StringComparer.OrdinalIgnoreCase);
        var themeChain = themeId is null
            ? null
            : _extensionManager.GetFeatureDependencies(themeId).Select(feature => feature.Id).Append(themeId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // The enabled features' assemblies, then the configured ones (which may limit a shell).
        var assemblies = new Dictionary<Assembly, ShellKind?>();
        foreach (var type in _blueprint.Dependencies.Keys)
        {
            assemblies.TryAdd(type.Assembly, null);
        }

        foreach (var (assembly, limit) in _options.Assemblies)
        {
            assemblies[assembly] = limit;
        }

        var admitted = new List<(int Order, ComponentDescriptor Descriptor)>();
        foreach (var (assembly, limit) in assemblies)
        {
            if (limit is { } only && only != shell)
            {
                continue;
            }

            var owner = OwnerOf(assembly);
            foreach (var descriptor in ComponentCatalogs.ForAssembly(assembly).Describe())
            {
                if (!TryGetFeature(descriptor, owner, enabled, out var feature))
                {
                    continue;
                }

                if (feature is not null && feature.IsTheme() && themeChain is not null && !themeChain.Contains(feature.Id))
                {
                    continue;
                }

                var position = feature is null ? -1 : order.GetValueOrDefault(feature.Id, int.MaxValue - 1);
                admitted.Add((position, descriptor with { Feature = feature?.Id }));
            }
        }

        var ordered = admitted.OrderBy(entry => entry.Order).Select(entry => entry.Descriptor).ToArray();
        return new ComponentRegistry([new FixedCatalog(ordered)], shell);
    }

    // The feature a component belongs to: its type's feature when its assembly is a feature's,
    // else the feature of the module its library is named after; null when nothing owns it.
    // False when that feature is disabled.
    private bool TryGetFeature(ComponentDescriptor descriptor, IExtensionInfo? owner, HashSet<string> enabled, out IFeatureInfo? feature)
    {
        feature = null;
        if (descriptor.Type is { } type)
        {
            if (_blueprint.Dependencies.TryGetValue(type, out var features) && features.Any())
            {
                feature = features.FirstOrDefault(candidate => candidate.Id == candidate.Extension.Id) ?? features.First();
                return true;
            }

            if (IsFeatureType(type))
            {
                // A type of a feature this tenant has not enabled.
                return false;
            }
        }

        if (owner is null)
        {
            return true;
        }

        feature = owner.Features.FirstOrDefault(candidate => candidate.Id == owner.Id) ?? owner.Features.FirstOrDefault();
        return feature is null || enabled.Contains(feature.Id);
    }

    private bool IsFeatureType(Type type)
    {
        try
        {
            return _typeFeatureProvider.GetFeaturesForDependency(type).Any();
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    // The module whose name prefixes the assembly's: Crest.Members.Member.BlazorWasm belongs to
    // Crest.Members, Crest.AdminTheme.Client to Crest.AdminTheme; the longest match wins.
    private IExtensionInfo? OwnerOf(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        return _extensionManager.GetExtensions()
            .Where(extension => name.Equals(extension.Id, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(extension.Id + ".", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(extension => extension.Id.Length)
            .FirstOrDefault();
    }

    private sealed class FixedCatalog(IReadOnlyList<ComponentDescriptor> descriptors) : IComponentCatalog
    {
        public IEnumerable<ComponentDescriptor> Describe() => descriptors;
    }
}
