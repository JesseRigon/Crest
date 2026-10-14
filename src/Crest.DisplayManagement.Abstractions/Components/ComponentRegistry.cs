namespace Crest.DisplayManagement.Components;

/// <summary>An assembly's components, generated at build time (trim-safe: no reflection at run time).</summary>
public interface IComponentCatalog
{
    IEnumerable<ComponentDescriptor> Describe();

    /// <summary>The open zone keys the assembly owns (decision 6).</summary>
    IEnumerable<ZoneKeyDescriptor> DescribeZoneKeys() => [];

    /// <summary>The components the assembly puts into open zone keys in code (decision 6).</summary>
    IEnumerable<ZoneContribution> DescribeContributions() => [];

    /// <summary>
    /// The assembly's module pages (§ 6a): its routed components. Pages are their own kind, never
    /// registry entries; the route table reads them from the same catalogs as the components.
    /// </summary>
    IEnumerable<PageDescriptor> DescribePages() => [];
}

/// <summary>A module page (§ 6a): a routed component, one route template, and whether it admits anonymous callers.</summary>
public sealed record PageDescriptor(Type Type, string Template, bool AllowsAnonymous);

/// <summary>
/// The one component registry (§ 1), per tenant and theme on the server, per shell in the
/// browser: what a tree may name. Replaces the attribute scan, the client shape bindings and the
/// page-region registry as discovery mechanisms.
/// </summary>
public interface IComponentRegistry
{
    IReadOnlyCollection<ComponentDescriptor> Components { get; }

    bool TryGet(string name, out ComponentDescriptor descriptor);

    /// <summary>The first registered name among <paramref name="alternates"/>, most specific (last) first, then the type's own segments.</summary>
    bool TryResolve(string type, IReadOnlyList<string> alternates, out ComponentDescriptor descriptor);

    /// <summary>Every open zone key (decision 6): the well-known ones and those the enabled modules declare.</summary>
    IReadOnlyCollection<ZoneKeyDescriptor> ZoneKeys { get; }

    /// <summary>The components modules put into <paramref name="key"/> in code, by priority.</summary>
    IReadOnlyList<ZoneContribution> Contributions(string key);
}

public static class ComponentRegistryExtensions
{
    /// <summary>The editor component for <paramref name="valueType"/>: the one that names it, else the one that edits anything (<c>*</c>).</summary>
    public static bool TryGetEditor(this IComponentRegistry registry, string valueType, out ComponentDescriptor editor)
    {
        editor = registry.Components.FirstOrDefault(component => component.Edits.Contains(valueType, StringComparer.OrdinalIgnoreCase))
            ?? registry.Components.FirstOrDefault(component => component.Edits.Contains("*"))!;
        return editor is not null;
    }

    /// <summary>The declaration of <paramref name="key"/>: an exact key first, then the family whose prefix it is under.</summary>
    public static bool TryGetZoneKey(this IComponentRegistry registry, string key, out ZoneKeyDescriptor zoneKey)
    {
        zoneKey = registry.ZoneKeys.FirstOrDefault(candidate => !candidate.IsFamily && candidate.Matches(key))
            ?? registry.ZoneKeys.Where(candidate => candidate.IsFamily && candidate.Matches(key)).OrderByDescending(candidate => candidate.Key.Length).FirstOrDefault()!;
        return zoneKey is not null;
    }
}

/// <summary>
/// The registry over a set of catalogs, filtered to one shell. Server and browser build it the
/// same way; only where the catalogs come from differs (the enabled features' assemblies on the
/// server, the loaded module assemblies in the browser).
/// </summary>
public sealed class ComponentRegistry : IComponentRegistry
{
    private static readonly string[] DisplayVariants = ["_Display__", "_SummaryAdmin", "_DetailAdmin", "_Summary", "_Detail"];

    private readonly Dictionary<string, ComponentDescriptor> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ZoneKeyDescriptor> _zoneKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ZoneContribution> _contributions = [];

    public ComponentRegistry(IEnumerable<IComponentCatalog> catalogs, ShellKind? shell = null)
    {
        foreach (var zoneKey in Crest.DisplayManagement.Components.ZoneKeys.WellKnown)
        {
            _zoneKeys[zoneKey.Key] = zoneKey;
        }

        foreach (var catalog in catalogs)
        {
            foreach (var descriptor in catalog.Describe())
            {
                if (shell is { } kind && descriptor.Shells.Count > 0 && !descriptor.Shells.Contains(kind))
                {
                    continue;
                }

                // A later catalog (a theme, a higher-priority feature) overrides an earlier one's name.
                _byName[descriptor.Name] = descriptor;
            }

            // A module declares only its own keys; a well-known key stays Crest's.
            foreach (var zoneKey in catalog.DescribeZoneKeys())
            {
                if (!_zoneKeys.TryGetValue(zoneKey.Key, out var existing) || existing.Module is not null)
                {
                    _zoneKeys[zoneKey.Key] = zoneKey;
                }
            }

            _contributions.AddRange(catalog.DescribeContributions());
        }
    }

    public IReadOnlyCollection<ComponentDescriptor> Components => _byName.Values;

    public IReadOnlyCollection<ZoneKeyDescriptor> ZoneKeys => _zoneKeys.Values;

    public IReadOnlyList<ZoneContribution> Contributions(string key) =>
        [.. _contributions.Where(contribution => string.Equals(contribution.Key, key, StringComparison.Ordinal)).OrderBy(contribution => contribution.Priority)];

    public bool TryGet(string name, out ComponentDescriptor descriptor) => _byName.TryGetValue(name, out descriptor!);

    public bool TryResolve(string type, IReadOnlyList<string> alternates, out ComponentDescriptor descriptor)
    {
        for (var i = alternates.Count - 1; i >= 0; i--)
        {
            if (TryGet(alternates[i], out descriptor))
            {
                return true;
            }
        }

        // "Content__BlogPost__Summary" → "Content__BlogPost" → "Content".
        for (var name = type; name.Length > 0; name = name.LastIndexOf("__", StringComparison.Ordinal) is var cut and > 0 ? name[..cut] : string.Empty)
        {
            if (TryGet(name, out descriptor))
            {
                return true;
            }
        }

        // A field's display mode ("TextField_Display__Header") or a part's display type shape
        // ("TitlePart_Summary") falls back to the shape's own component when the variant has none,
        // as the Razor views fell back to the shape's own view. Editor shapes never do.
        foreach (var marker in DisplayVariants)
        {
            var index = type.IndexOf(marker, StringComparison.Ordinal);
            if (index > 0 && TryGet(type[..index], out descriptor))
            {
                return true;
            }
        }

        descriptor = null!;
        return false;
    }
}
