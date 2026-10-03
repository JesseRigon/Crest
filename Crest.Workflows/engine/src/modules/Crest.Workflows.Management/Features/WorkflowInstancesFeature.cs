using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Management.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Management.Features;

/// <summary>
/// Enables storage of workflow instances.
/// </summary>
public class WorkflowInstancesFeature(IModule module) : FeatureBase(module)
{
    /// <summary>
    /// The factory to create new instances of <see cref="IWorkflowInstanceStore"/>.
    /// </summary>
    public Func<IServiceProvider, IWorkflowInstanceStore> WorkflowInstanceStore { get; set; } = sp => sp.GetRequiredService<MemoryWorkflowInstanceStore>();

    /// <inheritdoc />
    public override void Apply()
    {
        Services.AddScoped(WorkflowInstanceStore);
    }
}