namespace Crest.Workflows.Runtime.Results;

public record TriggerWorkflowsResult(ICollection<WorkflowExecutionResult> TriggeredWorkflows);