using System.Text.Json;
using Grove.Workflows.Http.Serialization;
using Grove.Workflows;
using Grove.Workflows.State;

namespace Grove.Workflows.Http.Handlers;

/// <summary>
/// Configures the serialization of <see cref="WorkflowState"/> objects.
/// </summary>
public class ConfigureWorkflowStateSerialization : SerializationOptionsConfiguratorBase
{
    public override void Configure(JsonSerializerOptions options)
    {
        options.Converters.Add(new HttpStatusCodeCaseForWorkflowInstanceConverter());
    }
}