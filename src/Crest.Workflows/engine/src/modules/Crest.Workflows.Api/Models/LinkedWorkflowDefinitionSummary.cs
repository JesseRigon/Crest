using Crest.Workflows.Models;
using Crest.Workflows.Management.Models;

namespace Crest.Workflows.Api.Models;

public class LinkedWorkflowDefinitionSummary : WorkflowDefinitionSummary
{
    public Link[]? Links { get; set; }
}
