using Grove.Workflows.Features.Services;
using Grove.Workflows.SasTokens.Features;

// ReSharper disable once CheckNamespace
namespace Grove.Workflows.Extensions;

/// <summary>
/// Provides extensions to install the <see cref="SasTokens"/> feature.
/// </summary>
public static class ModuleExtensions
{
    /// <summary>
    /// Install the <see cref="SasTokens"/> feature.
    /// </summary>
    public static IModule UseSasTokens(this IModule module, Action<SasTokensFeature>? configure = default)
    {
        module.Configure(configure);
        return module;
    }
}