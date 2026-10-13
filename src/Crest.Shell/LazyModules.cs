using System.Reflection;
using Crest.Components.Regions;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace Crest.Shell;

/// <summary>
/// Module client libraries loaded on demand, browser side, per shell.
/// </summary>
/// <remarks>
/// The host app's WASM build (Crest.LazyModules) works out which module client libraries
/// the app ships, which of them are lazy, which shell each belongs to
/// (<c>CrestShellAttribute</c>), the routes each serves, what each needs with it and which
/// contribute to page regions, and hands that here. A shell's router asks for a path's module before it matches
/// (<see cref="EnsureForPathAsync"/>) - only among ITS OWN shell's modules, so the admin
/// shell can never load a member page and the member shell never an admin one. A shell can
/// also load all of its modules up front (<see cref="EnsureShellAsync"/>), which the member
/// shell does to activate the seam implementations its modules supply. Each assembly loads
/// once per session.
///
/// Lives in the eager shell runtime rather than in a theme client so every shell can use it
/// without referencing another shell.
/// </remarks>
public sealed class LazyModules(
    string[] modules,
    (string Template, string Assembly, string Shell)[] routes,
    Dictionary<string, string[]> shellModules,
    Dictionary<string, string[]> dependencies,
    string[] regionContributors,
    Dictionary<string, string>? themes = null) : IPageRegionContributorLoader
{
    // The lazy theme UI of each shell (shell -> assembly), loaded when that shell's document
    // boots (docs/shells-and-themes.md › Theme UIs load per shell). Absent for a theme UI the
    // host keeps eager: it is already loaded.
    private readonly Dictionary<string, string> _themes = themes ?? new(StringComparer.Ordinal);
    private readonly List<Assembly> _loadedThemes = [];

    /// <summary>
    /// Every module client library the host app ships, eager and lazy, every shell's - the
    /// browser-side counterpart of the *.BlazorWasm assemblies the server loads.
    /// </summary>
    public IReadOnlyList<string> Modules => _enabled is null ? modules : [.. modules.Where(_enabled.Contains)];

    // The modules enabled for this tenant (docs/shells-and-themes.md › A disabled module is
    // absent for that tenant); null until the server said, meaning every shipped module.
    private HashSet<string>? _enabled;

    /// <summary>
    /// Limits this session to the module libraries enabled for the tenant, as the server lists
    /// them: a disabled module's routes, shell modules and region contributions are never
    /// loaded. Call before anything loads.
    /// </summary>
    public void Restrict(IEnumerable<string> enabledModules) =>
        _enabled = new HashSet<string>(enabledModules, StringComparer.OrdinalIgnoreCase);

    private bool IsEnabled(string module) => _enabled is null || _enabled.Contains(module);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly HashSet<string> _loadedNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Assembly> _loadedModules = [];
    private LazyAssemblyLoader? _loader;
    private bool _regionsLoaded;

    /// <summary>
    /// The module assemblies that were never lazy (loaded at boot), which the shell's component
    /// registry (components and page-region contributions) reads alongside <see cref="LoadedModules"/>. Set by the admin client,
    /// which is where the module registry lives.
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
            .Where(route => string.Equals(route.Shell, shell, StringComparison.Ordinal) && IsEnabled(route.Assembly) && Matches(route.Template, relative))
            .Select(route => route.Assembly)
            .Distinct()
            .ToArray();
        return LoadAsync(modules);
    }

    /// <summary>The theme UIs loaded so far (a shell's component registry reads its own).</summary>
    public IReadOnlyList<Assembly> LoadedThemes => _loadedThemes;

    /// <summary>
    /// Loads <paramref name="shell"/>'s theme UI if it is lazy and not loaded yet; true when this
    /// call loaded it.
    /// </summary>
    public Task<bool> EnsureThemeAsync(string shell) =>
        _themes.TryGetValue(shell, out var theme) ? LoadAsync([theme]) : Task.FromResult(false);

    /// <summary>Loads every module library belonging to <paramref name="shell"/>.</summary>
    public Task<bool> EnsureShellAsync(string shell) =>
        LoadAsync(shellModules.TryGetValue(shell, out var modules) ? [.. modules.Where(IsEnabled)] : []);

    public async Task EnsureLoadedAsync()
    {
        if (_regionsLoaded)
        {
            return;
        }

        await LoadAsync([.. regionContributors.Where(IsEnabled)]);
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

            var themeNames = _themes.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
            _loadedThemes.AddRange(loaded.Where(assembly => themeNames.Contains(assembly.GetName().Name ?? string.Empty)));
            _loadedModules.AddRange(loaded.Where(assembly => dependencies.ContainsKey(assembly.GetName().Name ?? string.Empty)
                && !themeNames.Contains(assembly.GetName().Name ?? string.Empty)));
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
