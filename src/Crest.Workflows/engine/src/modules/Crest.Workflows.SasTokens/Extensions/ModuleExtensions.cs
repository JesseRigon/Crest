using Crest.Workflows.Features.Services;
using Crest.Workflows.SasTokens.Features;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Extensions;

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