using Crest.Workflows.Platform.Models;

namespace Crest.Workflows.Platform.Services;

public interface IWorkflowExecutionContextHandler
{
    Task EvaluatingExpressionAsync(WorkflowExecutionExpressionContext context);
    Task EvaluatingScriptAsync(WorkflowExecutionScriptContext context);
    Task DehydrateValueAsync(SerializeWorkflowValueContext context);
    Task RehydrateValueAsync(SerializeWorkflowValueContext context);
}
