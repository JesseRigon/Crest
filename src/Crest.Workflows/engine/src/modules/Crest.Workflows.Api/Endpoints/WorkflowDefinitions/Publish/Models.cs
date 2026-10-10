using Crest.Workflows.Api.Models;

namespace Crest.Workflows.Api.Endpoints.WorkflowDefinitions.Publish;

internal class Request
{
    public string DefinitionId { get; set; } = default!;
}

internal record Response(LinkedWorkflowDefinitionModel WorkflowDefinition, bool AlreadyPublished, int ConsumingWorkflowCount);