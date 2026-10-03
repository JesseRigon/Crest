using Crest.Workflows;
using Crest.Workflows.Management.Models;
using Crest.Workflows.Parts;

namespace Crest.Workflows.Services;

public class WorkflowDefinitionPartSerializer(IApiSerializer apiSerializer, WorkflowDefinitionPartMapper mapper)
{
    public WorkflowDefinitionModel UpdateSerializedData(WorkflowDefinitionPart part)
    {
        var model = mapper.MapModel(part);
        part.SerializedData = apiSerializer.Serialize(model);
        return model;
    }

    /// <summary>Re-serializes a model a handler changed (publish-time stamps on the custom properties) so the change is what gets stored.</summary>
    public void Write(WorkflowDefinitionPart part, WorkflowDefinitionModel model) => part.SerializedData = apiSerializer.Serialize(model);
}