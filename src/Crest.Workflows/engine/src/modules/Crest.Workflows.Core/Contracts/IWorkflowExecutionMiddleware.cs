namespace Crest.Workflows;

public interface IWorkflowExecutionMiddleware
{
    ValueTask InvokeAsync(WorkflowExecutionContext context);
}