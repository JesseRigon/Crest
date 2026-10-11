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
    public Func<IServiceProvider, IWorkflowInstanceStore> WorkflowInstanceStore { get; set; } = sp => throw new InvalidOperationException("No IWorkflowInstanceStore configured. The host must supply one (Crest.Workflows wires the YesSql stores).");

    /// <inheritdoc />
    public override void Apply()
    {
        Services.AddScoped(WorkflowInstanceStore);
    }
}