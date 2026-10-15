using Grove.Workflows.Activities;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Management.Mappers;

namespace Grove.Workflows.Management.Materializers;

/// <summary>
/// A workflow materializer that deserializes workflows created in C# code.
/// </summary>
public class TypedWorkflowMaterializer(WorkflowDefinitionMapper workflowDefinitionMapper) : IWorkflowMaterializer
{
    /// <summary>
    /// The name of the materializer.
    /// </summary>
    public const string MaterializerName = "Typed";

    /// <inheritdoc />
    public string Name => MaterializerName;

    /// <inheritdoc />
    public ValueTask<Workflow> MaterializeAsync(WorkflowDefinition definition, CancellationToken cancellationToken)
    {
        var workflow = ToWorkflow(definition);
        return new ValueTask<Workflow>(workflow);
    }

    private Workflow ToWorkflow(WorkflowDefinition definition) => workflowDefinitionMapper.Map(definition);
}