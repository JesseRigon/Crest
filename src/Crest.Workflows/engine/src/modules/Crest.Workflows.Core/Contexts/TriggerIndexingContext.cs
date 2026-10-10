using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;

namespace Crest.Workflows;

public class TriggerIndexingContext(WorkflowIndexingContext workflowIndexingContext, ExpressionExecutionContext expressionExecutionContext, ITrigger trigger, CancellationToken cancellationToken)
{
    public WorkflowIndexingContext WorkflowIndexingContext { get; } = workflowIndexingContext;
    public ExpressionExecutionContext ExpressionExecutionContext { get; } = expressionExecutionContext;
    public ITrigger Trigger { get; } = trigger;
    public CancellationToken CancellationToken { get; } = cancellationToken;
    public string TriggerName { get; set; } = trigger.Type;

    public T? Get<T>(Input<T>? input) => ExpressionExecutionContext.Get(input);
}