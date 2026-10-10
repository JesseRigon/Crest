namespace Crest.Workflows.Platform.Handlers;

using Crest.Workflows.Platform.Models;
using Crest.Workflows.Platform.Scripting;
using Crest.Workflows.Platform.Services;

public class DefaultWorkflowExecutionContextHandler : WorkflowExecutionContextHandlerBase
{
    public override Task EvaluatingScriptAsync(WorkflowExecutionScriptContext context)
    {
        context.ScopedMethodProviders.Add(new WorkflowMethodsProvider(context.WorkflowContext));
        return Task.CompletedTask;
    }
}
