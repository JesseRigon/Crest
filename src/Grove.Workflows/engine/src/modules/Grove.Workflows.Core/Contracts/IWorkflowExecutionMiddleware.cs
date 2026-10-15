namespace Grove.Workflows;

public interface IWorkflowExecutionMiddleware
{
    ValueTask InvokeAsync(WorkflowExecutionContext context);
}