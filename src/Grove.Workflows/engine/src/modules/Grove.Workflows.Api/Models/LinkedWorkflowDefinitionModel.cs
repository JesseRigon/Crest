using Grove.Workflows.Models;
using Grove.Workflows.Management.Models;

namespace Grove.Workflows.Api.Models;

public class LinkedWorkflowDefinitionModel(Link[]? links) : WorkflowDefinitionModel
{
    public Link[]? Links { get; init; } = links;
}
