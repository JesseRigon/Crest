using Grove.Workflows.Pipelines.WorkflowExecution;

namespace Grove.Workflows.Runtime.Middleware.Workflows;

public class WorkflowHeartbeatMiddleware(WorkflowMiddlewareDelegate next, WorkflowHeartbeatGeneratorFactory workflowHeartbeatGeneratorFactory) : WorkflowExecutionMiddleware(next)
{
    public override async ValueTask InvokeAsync(WorkflowExecutionContext context)
    {
        using var heartbeat = workflowHeartbeatGeneratorFactory.CreateHeartbeatGenerator(context);
        await Next(context);
    }
}