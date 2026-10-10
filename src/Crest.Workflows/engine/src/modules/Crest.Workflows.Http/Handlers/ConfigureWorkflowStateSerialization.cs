using System.Text.Json;
using Crest.Workflows.Http.Serialization;
using Crest.Workflows;
using Crest.Workflows.State;

namespace Crest.Workflows.Http.Handlers;

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