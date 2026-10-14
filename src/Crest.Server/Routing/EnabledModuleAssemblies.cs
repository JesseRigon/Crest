using System.Reflection;
using Crest.Environment.Extensions;
using Crest.Environment.Shell;

namespace Crest.Routing;

/// <summary>
/// Shell-lifetime cache of <see cref="IEnabledModuleAssemblies"/>'s answer. The tenant's
/// container is rebuilt when a feature is enabled or disabled, which drops this with it.
/// </summary>
public sealed class EnabledModuleAssembliesCache
{
    internal Task<IReadOnlySet<string>>? Enabled { get; set; }

    internal readonly Lock Gate = new();
}

public sealed class EnabledModuleAssemblies(
    IShellFeaturesManager shellFeaturesManager,
    IExtensionManager extensionManager,
    EnabledModuleAssembliesCache cache) : IEnabledModuleAssemblies
{
    public Task<IReadOnlySet<string>> GetEnabledAsync()
    {
        lock (cache.Gate)
        {
            return cache.Enabled ??= BuildAsync();
        }
    }

    public async Task<bool> IsEnabledAsync(Assembly assembly)
    {
        var name = assembly.GetName().Name;
        return !ModuleClientAssemblies.IsModuleClient(name) || (await GetEnabledAsync()).Contains(name!);
    }

    private async Task<IReadOnlySet<string>> BuildAsync()
    {
        var enabledModules = (await shellFeaturesManager.GetEnabledFeaturesAsync())
            .Select(feature => feature.Extension.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var extensionIds = extensionManager.GetExtensions().Select(extension => extension.Id).ToArray();

        // A library whose module is not installed at all belongs to no enabled module and is
        // absent too: fail closed, a page nobody can enable is not served.
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetName().Name)
            .Where(ModuleClientAssemblies.IsModuleClient)
            .OfType<string>()
            .Where(name => ModuleClientAssemblies.OwningExtensionId(name, extensionIds) is { } owner && enabledModules.Contains(owner))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
