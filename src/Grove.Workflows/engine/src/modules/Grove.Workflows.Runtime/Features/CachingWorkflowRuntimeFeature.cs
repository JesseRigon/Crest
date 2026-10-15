using Grove.Workflows.Caching.Features;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Attributes;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Runtime.Handlers;
using Grove.Workflows.Runtime.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Runtime.Features;

/// <summary>
/// Installs and configures workflow runtime caching features.
/// </summary>
[DependsOn(typeof(MemoryCacheFeature))]
public class CachingWorkflowRuntimeFeature : FeatureBase
{
    /// <inheritdoc />
    public CachingWorkflowRuntimeFeature(IModule module) : base(module)
    {
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services
            // Decorators.
            .Decorate<ITriggerStore, CachingTriggerStore>()

            // Handlers.
            .AddNotificationHandler<InvalidateTriggersCache>()
            .AddNotificationHandler<InvalidateWorkflowsCache>();
    }
}