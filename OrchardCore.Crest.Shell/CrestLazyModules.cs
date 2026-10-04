using System.Reflection;
using Crest.Components.Regions;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace Crest.Shell;

/// <summary>
/// Module client libraries loaded on demand, browser side, per shell.
/// </summary>
/// <remarks>
/// OrchardCore.Crest.Client's build works out which module assemblies are lazy, which shell
/// each belongs to (<c>CrestShellAttribute</c>), the routes each serves, what each needs
/// with it and which contribute to page regions (CrestLazyModules.targets), and hands that
/// here. A shell's router asks for a path's module before it matches
/// (<see cref="EnsureForPathAsync"/>) - only among ITS OWN shell's modules, so the admin
/// shell can never load a member page and the member shell never an admin one. A shell can
/// also load all of its modules up front (<see cref="EnsureShellAsync"/>), which the member
/// shell does to activate the seam implementations its modules supply. Each assembly loads
/// once per session.
///
/// Lives in the eager shell runtime rather than in a theme client so every shell can use it
/// without referencing another shell.
/// </remarks>
public sealed class CrestLazyModules(
    (string Template, string Assembly, string Shell)[] routes,
    Dictionary<string, string[]> shellModules,
    Dictionary<string, string[]> dependencies,
    string[] regionContributors) : IPageRegionContributorLoader
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HashSet<string> _loadedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Assembly> _loadedModules = [];
    private LazyAssemblyLoader? _loader;
    private bool _regionsLoaded;

    /// <summary>
    /// The module assemblies that were never lazy (loaded at boot), which page-region
    /// discovery runs over alongside <see cref="LoadedModules"/>. Set by the admin client,
    /// which is where the build's eager module registry lives.
    /// </summary>
    public IReadOnlyList<Assembly> EagerModules { get; set; } = [];

    /// <summary>Module assemblies loaded so far, every shell's (a router filters by shell).</summary>
    public IReadOnlyList<Assembly> LoadedModules => _loadedModules;

    /// <summary>Raised after a load added assemblies.</summary>
    public event Action? ModulesLoaded;

    /// <summary>Whether a browser loader is attached - false on the server, where every assembly is already loaded.</summary>
    public bool IsAttached => _loader is not null;

    public void Attach(LazyAssemblyLoader loader) => _loader ??= loader;

    /// <summary>
    /// Loads the <paramref name="shell"/> module serving <paramref name="path"/> (relative to
    /// that shell's base), if it is lazy and not loaded yet.
    /// </summary>
    public Task<bool> EnsureForPathAsync(string path, string shell)
    {
        var relative = "/" + path.Split('?', '#')[0].Trim('/');
        var modules = routes
            .Where(route => string.Equals(route.Shell, shell, StringComparison.Ordinal) && Matches(route.Template, relative))
            .Select(route => route.Assembly)
            .Distinct()
            .ToArray();
        return LoadAsync(modules);
    }

    /// <summary>Loads every module library belonging to <paramref name="shell"/>.</summary>
    public Task<bool> EnsureShellAsync(string shell) =>
        LoadAsync(shellModules.TryGetValue(shell, out var modules) ? modules : []);

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
            PageRegionRegistry.Configure(EagerModules.Concat(_loadedModules));
            ModulesLoaded?.Invoke();
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    // Blazor route templates: literal segments, {parameter[:constraint][?]} and {*catchAll}.
    public static bool Matches(string template, string path)
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
