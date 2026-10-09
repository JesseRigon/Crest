using Crest.Scripting;

namespace Crest.Workflows.Platform.Models;

public class WorkflowExecutionScriptContext : WorkflowExecutionHandlerContextBase
{
    public WorkflowExecutionScriptContext(WorkflowExecutionContext workflowContext) : base(workflowContext)
    {
    }

    public IList<IGlobalMethodProvider> ScopedMethodProviders { get; init; } = [];
}
