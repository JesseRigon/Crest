using Grove.Workflows;
using Grove.Workflows.Management.Models;
using Grove.Workflows.FieldSets;

namespace Grove.Workflows.Services;

public class WorkflowDefinitionFieldSetSerializer(IApiSerializer apiSerializer, WorkflowDefinitionFieldSetMapper mapper)
{
    public WorkflowDefinitionModel UpdateSerializedData(WorkflowDefinitionFieldSet part)
    {
        var model = mapper.MapModel(part);
        part.SerializedData = apiSerializer.Serialize(model);
        return model;
    }

    /// <summary>Re-serializes a model a handler changed (publish-time stamps on the custom properties) so the change is what gets stored.</summary>
    public void Write(WorkflowDefinitionFieldSet part, WorkflowDefinitionModel model) => part.SerializedData = apiSerializer.Serialize(model);
}