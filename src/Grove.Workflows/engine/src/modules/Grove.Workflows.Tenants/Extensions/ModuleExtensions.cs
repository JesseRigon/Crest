using Grove.Workflows.Features.Services;
using Grove.Workflows.Tenants.Features;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace Grove.Workflows.Tenants.Extensions;

/// <summary>
/// Extensions for <see cref="IModule"/> that installs the <see cref="TenantsFeature"/> feature.
/// </summary>
[UsedImplicitly]
public static class ModuleExtensions
{
    /// <summary>
    /// Installs and configures the <see cref="TenantsFeature"/> feature.
    /// </summary>
    [UsedImplicitly]
    public static IModule UseTenants(this IModule module, Action<TenantsFeature>? configure = null)
    {
        module.Configure(configure);
        return module;
    }
}