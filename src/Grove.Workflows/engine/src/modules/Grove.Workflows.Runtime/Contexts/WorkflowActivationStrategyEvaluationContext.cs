using Grove.Workflows.Activities;

namespace Grove.Workflows.Runtime;

public class WorkflowActivationStrategyEvaluationContext
{
    public Workflow Workflow { get; set; } = default!;
    public string? CorrelationId { get; set; }
    public CancellationToken CancellationToken { get; set; }
}