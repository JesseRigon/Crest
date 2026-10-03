using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Management.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Management.Features;

/// <summary>
/// Configures workflow definition storage.
/// </summary>
public class WorkflowDefinitionsFeature(IModule module) : FeatureBase(module)
{
    /// <summary>
    /// The factory to create new instances of <see cref="IWorkflowDefinitionStore"/>.
    /// </summary>
    public Func<IServiceProvider, IWorkflowDefinitionStore> WorkflowDefinitionStore { get; set; } = sp => sp.GetRequiredService<MemoryWorkflowDefinitionStore>();

    /// <inheritdoc />
    public override void Apply()
    {
        Services
            .AddScoped(WorkflowDefinitionStore);
    }
}