using Grove.Workflows.Activities;
using Grove.Workflows.Management.Materializers;

namespace Grove.Workflows.Runtime;

/// <inheritdoc />
public class DefaultWorkflowRegistry(IWorkflowDefinitionStorePopulator populator) : IWorkflowRegistry
{
    /// <inheritdoc />
    public async Task RegisterAsync(Workflow workflow, CancellationToken cancellationToken = default)
    {
        var materializedWorkflow = new MaterializedWorkflow(workflow, nameof(DefaultWorkflowRegistry), TypedWorkflowMaterializer.MaterializerName);
        var result = await populator.AddAsync(materializedWorkflow, cancellationToken);
        result.ThrowIfFailure();
    }
}