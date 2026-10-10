using Crest.Workflows.Models;

namespace Crest.Workflows.Runtime.Exceptions;

public class WorkflowGraphNotFoundException(string message, WorkflowDefinitionHandle workflowDefinitionHandle) : Exception(message)
{
    public WorkflowDefinitionHandle WorkflowDefinitionHandle { get; } = workflowDefinitionHandle;
}