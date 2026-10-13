namespace Crest.DisplayManagement.Components;

/// <summary>An assembly's components, generated at build time (trim-safe: no reflection at run time).</summary>
public interface IComponentCatalog
{
    IEnumerable<ComponentDescriptor> Describe();
}

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
}

/// <summary>
/// The registry over a set of catalogs, filtered to one shell. Server and browser build it the
/// same way; only where the catalogs come from differs (the enabled features' assemblies on the
/// server, the loaded module assemblies in the browser).
/// </summary>
public sealed class ComponentRegistry : IComponentRegistry
{
    private readonly Dictionary<string, ComponentDescriptor> _byName = new(StringComparer.OrdinalIgnoreCase);

    public ComponentRegistry(IEnumerable<IComponentCatalog> catalogs, ShellKind? shell = null)
    {
        foreach (var descriptor in catalogs.SelectMany(catalog => catalog.Describe()))
        {
            if (shell is { } kind && descriptor.Shells.Count > 0 && !descriptor.Shells.Contains(kind))
            {
                continue;
            }

            // A later catalog (a theme, a higher-priority feature) overrides an earlier one's name.
            _byName[descriptor.Name] = descriptor;
        }
    }

    public IReadOnlyCollection<ComponentDescriptor> Components => _byName.Values;

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

        descriptor = null!;
        return false;
    }
}
