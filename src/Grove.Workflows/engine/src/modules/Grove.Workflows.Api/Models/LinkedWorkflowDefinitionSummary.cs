using Grove.Workflows.Models;
using Grove.Workflows.Management.Models;

namespace Grove.Workflows.Api.Models;

public class LinkedWorkflowDefinitionSummary : WorkflowDefinitionSummary
{
    public Link[]? Links { get; set; }
}
