using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Management.Handlers.Notifications;
using Crest.Workflows.Management.Services;
using Crest.Workflows.Management.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Management.Features;

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