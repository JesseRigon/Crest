namespace Crest.Workflows.Platform.Models;

public class WorkflowTypeContext
{
    public WorkflowTypeContext(WorkflowType workflowType)
    {
        WorkflowType = workflowType;
    }

    public WorkflowType WorkflowType { get; }
}
