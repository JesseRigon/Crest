using System.Text.Json;
using Crest.Workflows.Activities;
using Crest.Workflows.Management.Mappers;
using Crest.Workflows.Management.Models;

namespace Crest.Workflows.Management.Services;

/// <summary>
/// A serializer that parses JSON.
/// </summary>
public class WorkflowSerializer(IApiSerializer apiSerializer, WorkflowDefinitionMapper workflowDefinitionMapper) : IWorkflowSerializer
{
    private JsonSerializerOptions? _writeOptions;

    /// <inheritdoc />
    public string Serialize(Workflow workflow)
    {
        var model = workflowDefinitionMapper.Map(workflow);
        var serializerOptions = GetWriteOptionsInternal();
        return JsonSerializer.Serialize(model, serializerOptions);
    }

    /// <inheritdoc />
    public Workflow Deserialize(string serializedWorkflow)
    {
        var model = apiSerializer.Deserialize<WorkflowDefinitionModel>(serializedWorkflow);
        return workflowDefinitionMapper.Map(model);
    }

    private JsonSerializerOptions GetWriteOptionsInternal()
    {
        if (_writeOptions != null)
            return _writeOptions;

        var options = apiSerializer.GetOptions();
        return _writeOptions = options;
    }
}