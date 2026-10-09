using System.Text.Encodings.Web;
using Crest.Workflows.Platform.Models;

namespace Crest.Workflows.Platform.Services;

public interface IWorkflowExpressionEvaluator
{
    Task<T> EvaluateAsync<T>(WorkflowExpression<T> expression, WorkflowExecutionContext workflowContext, TextEncoder encoder);
}
