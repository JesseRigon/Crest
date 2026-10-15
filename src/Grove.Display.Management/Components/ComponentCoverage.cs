#nullable enable

using Grove.Display.Management.Descriptors;

namespace Grove.Display.Management.Components;

/// <summary>One shape type in the coverage report: what renders it on the display tree.</summary>
/// <param name="ShapeType">The shape type (a descriptor of the shape table, or a type a display tree fell back on).</param>
/// <param name="Component">The component the type resolves to in the shell's registry; null when uncovered.</param>
/// <param name="Bindings">The bindings of the type and its alternates without a component.</param>
/// <param name="Fallbacks">How many times a display tree met the type without a component since the tenant started.</param>
/// <param name="LastPath">The request path of the last time.</param>
public sealed record ShapeCoverage(string ShapeType, string? Component, string[] Bindings, long Fallbacks, string? LastPath)
{
    public bool Covered => Component is not null;
}

/// <summary>
/// The coverage report (docs/MVC/shape-engine.md, C2): every shape type the shape table knows
/// (its descriptors and their bindings) and every type a display tree fell back on, against the
/// component registry of a shell. Generated, so the coverage table is never hand-kept.
/// </summary>
public sealed class ComponentCoverage(IComponentRegistryManager registries, IShapeTableManager tables, DisplayFallbackLog fallbacks)
{
    public async Task<IReadOnlyList<ShapeCoverage>> ReportAsync(ShellKind shell, string? themeId = null)
    {
        var registry = registries.GetRegistry(shell, themeId);
        var table = await tables.GetShapeTableAsync(themeId);

        var types = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in table.Descriptors.Keys)
        {
            types.TryAdd(type, []);
        }

        foreach (var (name, binding) in table.Bindings)
        {
            if (!string.IsNullOrEmpty(binding.ComponentName) || registry.TryGet(name, out _))
            {
                continue;
            }

            var index = name.IndexOf("__", StringComparison.Ordinal);
            var type = index > 0 ? name[..index] : name;
            if (!types.TryGetValue(type, out var names))
            {
                types[type] = names = [];
            }

            names.Add($"{name} ({binding.BindingSource})");
        }

        foreach (var type in fallbacks.Entries.Keys)
        {
            types.TryAdd(type, []);
        }

        return types
            .Select(entry =>
            {
                var component = registry.TryResolve(entry.Key, [], out var descriptor) ? descriptor.Name : null;
                fallbacks.Entries.TryGetValue(entry.Key, out var fallback);
                return new ShapeCoverage(entry.Key, component, [.. entry.Value.Order(StringComparer.OrdinalIgnoreCase)], fallback?.Count ?? 0, fallback?.LastPath);
            })
            // A type nothing binds to HTML and nothing fell back on renders nothing to cover.
            .Where(entry => entry.Covered || entry.Bindings.Length > 0 || entry.Fallbacks > 0)
            .OrderBy(entry => entry.Covered)
            .ThenByDescending(entry => entry.Fallbacks)
            .ThenBy(entry => entry.ShapeType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
