using Fluid;

namespace Crest.Workflows.Platform.Models;

public class WorkflowExecutionExpressionContext : WorkflowExecutionHandlerContextBase
{
    public WorkflowExecutionExpressionContext(TemplateContext templateContext, WorkflowExecutionContext workflowExecutionContext) : base(workflowExecutionContext)
    {
        TemplateContext = templateContext;
    }

    public TemplateContext TemplateContext { get; }
}
