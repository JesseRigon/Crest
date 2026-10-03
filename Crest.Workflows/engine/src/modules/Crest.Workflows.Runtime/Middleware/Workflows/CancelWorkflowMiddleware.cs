using Crest.Workflows.Pipelines.WorkflowExecution;

namespace Crest.Workflows.Runtime.Middleware.Workflows;

/// <summary>
/// Represents a middleware that cancels the workflow.
/// </summary>
public class CancelWorkflowMiddleware(WorkflowMiddlewareDelegate next) : WorkflowExecutionMiddleware(next)
{
    /// <inheritdoc />
    public override ValueTask InvokeAsync(WorkflowExecutionContext context)
    {
        context.Cancel();
        return ValueTask.CompletedTask;
    }
}