using Crest.Workflows.Features.Services;
using Crest.Workflows.Tenants.Features;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Tenants.Extensions;

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