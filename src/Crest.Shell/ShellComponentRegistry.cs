using System.Reflection;
using Crest.DisplayManagement.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Shell;

/// <summary>
/// The component registry in the browser (docs/blazor-display.md § 1), for the one shell this
/// document runs: the shell's own assemblies (the shared component libraries and its theme
/// client), the module libraries loaded at boot and those <see cref="LazyModules"/> loads
/// later. A module library counts only for the shell it belongs to (<see cref="ShellAssemblyFilter"/>),
/// the same isolation its router applies, and the registry is rebuilt when a lazy module loads.
/// </summary>
/// <remarks>
/// Server and browser build the registry the same way (<see cref="ComponentRegistry"/> over the
/// assemblies' catalogs); the server's is per tenant and filtered by the enabled features, and the
/// browser only ever has the libraries the server let it load.
/// </remarks>
public sealed class ShellComponentRegistry : IComponentRegistry, IDisposable
{
    private readonly string _shell;
    private readonly ShellKind _kind;
    private readonly IReadOnlyList<Assembly> _shellAssemblies;
    private readonly LazyModules? _modules;
    private ComponentRegistry _current;

    public ShellComponentRegistry(string shell, IEnumerable<Assembly> shellAssemblies, LazyModules? modules = null)
    {
        _shell = shell;
        _kind = ComponentSchema.ParseShell(shell) ?? ShellKind.Site;
        _shellAssemblies = shellAssemblies.Distinct().ToArray();
        _modules = modules;
        _current = Build();

        if (_modules is not null)
        {
            _modules.ModulesLoaded += Refresh;
        }
    }

    /// <summary>Raised after a lazy module load changed the registry.</summary>
    public event Action? Changed;

    public IReadOnlyCollection<ComponentDescriptor> Components => _current.Components;

    public bool TryGet(string name, out ComponentDescriptor descriptor) => _current.TryGet(name, out descriptor);

    public bool TryResolve(string type, IReadOnlyList<string> alternates, out ComponentDescriptor descriptor) =>
        _current.TryResolve(type, alternates, out descriptor);

    public IReadOnlyCollection<ZoneKeyDescriptor> ZoneKeys => _current.ZoneKeys;

    public IReadOnlyList<ZoneContribution> Contributions(string key) => _current.Contributions(key);

    public void Dispose()
    {
        if (_modules is not null)
        {
            _modules.ModulesLoaded -= Refresh;
        }
    }

    private void Refresh()
    {
        _current = Build();
        Changed?.Invoke();
    }

    // Shared libraries first, then the shell's modules in load order: a later catalog overrides
    // an earlier one's name, as on the server.
    private ComponentRegistry Build()
    {
        // The shell's theme UI arrives like a module, when its shell boots (a lazy theme client).
        var modules = _modules is null
            ? Enumerable.Empty<Assembly>()
            : _modules.LoadedThemes.Concat(_modules.EagerModules).Concat(_modules.LoadedModules)
                .Where(assembly => ShellAssemblyFilter.BelongsTo(assembly, _shell));

        var catalogs = _shellAssemblies.Concat(modules)
            .Distinct()
            .Select(ComponentCatalogs.ForAssembly)
            .ToArray();

        return new ComponentRegistry(catalogs, _kind);
    }
}

public static class ShellComponentRegistryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the browser's <see cref="IComponentRegistry"/> for <paramref name="shell"/> over
    /// <paramref name="shellAssemblies"/> plus the module libraries of the registered
    /// <see cref="LazyModules"/>, if any. What <see cref="DisplayTreeRenderer"/> injects.
    /// </summary>
    public static IServiceCollection AddShellComponentRegistry(this IServiceCollection services, string shell, IEnumerable<Assembly> shellAssemblies)
    {
        var assemblies = shellAssemblies.ToArray();
        services.AddSingleton(sp => new ShellComponentRegistry(shell, assemblies, sp.GetService<LazyModules>()));
        services.AddSingleton<IComponentRegistry>(sp => sp.GetRequiredService<ShellComponentRegistry>());
        return services;
    }
}
