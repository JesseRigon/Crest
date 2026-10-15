namespace Grove.Workflows.Runtime.Results;

public record TriggerWorkflowsResult(ICollection<WorkflowExecutionResult> TriggeredWorkflows);