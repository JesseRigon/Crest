using Crest.Workflows.Models;
using Crest.Workflows.Management.Models;

namespace Crest.Workflows.Api.Models;

public class LinkedWorkflowDefinitionModel(Link[]? links) : WorkflowDefinitionModel
{
    public Link[]? Links { get; init; } = links;
}
