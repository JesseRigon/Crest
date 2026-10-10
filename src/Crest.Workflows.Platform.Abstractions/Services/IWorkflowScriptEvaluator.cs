using Crest.Scripting;
using Crest.Workflows.Platform.Models;

namespace Crest.Workflows.Platform.Services;

public interface IWorkflowScriptEvaluator
{
    Task<T> EvaluateAsync<T>(WorkflowExpression<T> expression, WorkflowExecutionContext workflowContext, params IGlobalMethodProvider[] scopedMethodProviders);
}
