using System.Reflection;
using Crest.Components.Regions;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace Crest.Admin;

/// <summary>
/// Module pages loaded on demand, browser side. OrchardCore.Crest.Client's build works out
/// which module assemblies are lazy, the routes each serves, what each needs with it and
/// which contribute to page regions (CrestLazyModules.targets) and hands that here; the
/// admin router asks for a path's module before it matches (<see cref="EnsureForPathAsync"/>),
/// a page region for the region contributors. Each assembly loads once per session.
/// </summary>
public sealed class CrestLazyModules(
    (string Template, string Assembly)[] routes,
    Dictionary<string, string[]> dependencies,
    string[] regionContributors) : IPageRegionContributorLoader
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HashSet<string> _loadedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Assembly> _loadedModules = [];
    private LazyAssemblyLoader? _loader;
    private bool _regionsLoaded;

    /// <summary>Module assemblies loaded so far (the router's additional assemblies grow with them).</summary>
    public IReadOnlyList<Assembly> LoadedModules => _loadedModules;

    public event Action? ModulesLoaded;

    internal void Attach(LazyAssemblyLoader loader) => _loader ??= loader;

    /// <summary>Loads the module serving <paramref name="path"/> (relative to the admin base), if it is lazy and not loaded.</summary>
    public Task<bool> EnsureForPathAsync(string path)
    {
        var relative = "/" + path.Split('?', '#')[0].Trim('/');
        var modules = routes.Where(route => Matches(route.Template, relative)).Select(route => route.Assembly).Distinct().ToArray();
        return LoadAsync(modules);
    }

    public async Task EnsureLoadedAsync()
    {
        if (_regionsLoaded)
        {
            return;
        }

        await LoadAsync(regionContributors);
        _regionsLoaded = true;
    }

    private async Task<bool> LoadAsync(IReadOnlyCollection<string> modules)
    {
        if (_loader is null || modules.All(_loadedNames.Contains))
        {
            return false;
        }

        await _lock.WaitAsync();
        try
        {
            var names = modules.Where(module => !_loadedNames.Contains(module))
                .SelectMany(module => dependencies.TryGetValue(module, out var needed) ? needed : [module])
                .Where(name => !_loadedNames.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (names.Length == 0)
            {
                return false;
            }

            var loaded = await _loader.LoadAssembliesAsync(names.Select(name => $"{name}.wasm"));
            foreach (var name in names)
            {
                _loadedNames.Add(name);
            }

            _loadedModules.AddRange(loaded.Where(assembly => dependencies.ContainsKey(assembly.GetName().Name ?? string.Empty)));
            PageRegionRegistry.Configure(CrestModuleAssemblyRegistry.Assemblies.Concat(_loadedModules));
            ModulesLoaded?.Invoke();
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Blazor route templates: literal segments, {parameter[:constraint][?]} and {*catchAll}.
    internal static bool Matches(string template, string path)
    {
        var templateSegments = template.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var pathSegments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < templateSegments.Length; index++)
        {
            var segment = templateSegments[index];
            var isParameter = segment.StartsWith('{') && segment.EndsWith('}');
            if (isParameter && segment.StartsWith("{*", StringComparison.Ordinal))
            {
                return true;
            }

            if (index >= pathSegments.Length)
            {
                return isParameter && segment.EndsWith("?}", StringComparison.Ordinal)
                    && templateSegments.Skip(index).All(rest => rest.EndsWith("?}", StringComparison.Ordinal));
            }

            if (!isParameter && !string.Equals(segment, pathSegments[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return templateSegments.Length == pathSegments.Length;
    }
}
