using Crest.Workflows.Caching.Features;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Runtime.Handlers;
using Crest.Workflows.Runtime.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Runtime.Features;

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