using Crest.Workflows.Caching.Options;
using Crest.Workflows.Caching.Services;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Caching.Features;

/// <summary>
/// Configures the MemoryCache.
/// </summary>
public class MemoryCacheFeature(IModule module) : FeatureBase(module)
{
    /// <summary>
    /// A delegate to configure the <see cref="CachingOptions"/>.
    /// </summary>
    public Action<CachingOptions> CachingOptions { get; set; } = _ => { };

    /// <inheritdoc />
    public override void Apply()
    {
        Services.Configure(CachingOptions);

        Services
            .AddMemoryCache()
            .AddSingleton<ICacheManager, CacheManager>()
            .AddSingleton<IChangeTokenSignalInvoker, ChangeTokenSignalInvoker>()
            .AddSingleton<IChangeTokenSignaler, ChangeTokenSignaler>()
            ;
    }
}