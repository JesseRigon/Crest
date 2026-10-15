using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Management.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Management.Features;

/// <summary>
/// Enables storage of workflow instances.
/// </summary>
public class WorkflowInstancesFeature(IModule module) : FeatureBase(module)
{
    /// <summary>
    /// The factory to create new instances of <see cref="IWorkflowInstanceStore"/>.
    /// </summary>
    public Func<IServiceProvider, IWorkflowInstanceStore> WorkflowInstanceStore { get; set; } = sp => throw new InvalidOperationException("No IWorkflowInstanceStore configured. The host must supply one (Grove.Workflows wires the YesSql stores).");

    /// <inheritdoc />
    public override void Apply()
    {
        Services.AddScoped(WorkflowInstanceStore);
    }
}