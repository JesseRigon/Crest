using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Management.Handlers.Notifications;
using Grove.Workflows.Management.Services;
using Grove.Workflows.Management.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Management.Features;

/// <summary>
/// Configures workflow definition caching.
/// </summary>
public class CachingWorkflowDefinitionsFeature : FeatureBase
{
    /// <inheritdoc />
    public CachingWorkflowDefinitionsFeature(IModule module) : base(module)
    {
    }
    
    /// <inheritdoc />
    public override void Apply()
    {
        Services.AddSingleton<IWorkflowDefinitionCacheManager, WorkflowDefinitionCacheManager>();
        Services.Decorate<IWorkflowDefinitionStore, CachingWorkflowDefinitionStore>();
        Services.Decorate<IWorkflowDefinitionService, CachingWorkflowDefinitionService>();
        Services.AddNotificationHandler<EvictWorkflowDefinitionServiceCache>();
    }
}