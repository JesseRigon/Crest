using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Management.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Management.Features;

/// <summary>
/// Configures workflow definition storage.
/// </summary>
public class WorkflowDefinitionsFeature(IModule module) : FeatureBase(module)
{
    /// <summary>
    /// The factory to create new instances of <see cref="IWorkflowDefinitionStore"/>.
    /// </summary>
    public Func<IServiceProvider, IWorkflowDefinitionStore> WorkflowDefinitionStore { get; set; } = sp => throw new InvalidOperationException("No IWorkflowDefinitionStore configured. The host must supply one (Grove.Workflows wires the YesSql stores).");

    /// <inheritdoc />
    public override void Apply()
    {
        Services
            .AddScoped(WorkflowDefinitionStore);
    }
}