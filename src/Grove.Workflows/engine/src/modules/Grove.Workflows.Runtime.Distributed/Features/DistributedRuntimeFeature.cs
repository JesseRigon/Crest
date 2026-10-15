using Grove.Workflows.Extensions;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Attributes;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Resilience.Features;
using Grove.Workflows.Runtime.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Runtime.Distributed.Features;

/// <summary>
/// Installs and configures workflow runtime features.
/// </summary>
[DependsOn(typeof(WorkflowRuntimeFeature))]
[DependsOn(typeof(ResilienceFeature))]
public class DistributedRuntimeFeature(IModule module) : FeatureBase(module)
{
    public override void Configure()
    {
        Module.UseWorkflowRuntime(runtime =>
        {
            runtime.WorkflowRuntime = sp => sp.GetRequiredService<DistributedWorkflowRuntime>();
            runtime.BookmarkQueueWorker = sp => sp.GetRequiredService<DistributedBookmarkQueueWorker>();
        });
    }

    /// <inheritdoc />
    public override void Apply()
    {
        Services
            .AddScoped<DistributedWorkflowRuntime>()
            .AddScoped<DistributedBookmarkQueueWorker>()
            
            .Decorate<IWorkflowDefinitionsRefresher, DistributedWorkflowDefinitionsRefresher>()
            .Decorate<IWorkflowDefinitionsReloader, DistributedWorkflowDefinitionsReloader>();
    }
}