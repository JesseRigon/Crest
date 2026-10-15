using Grove.Workflows.Activities;

namespace Grove.Workflows.Runtime;

public class InvokeTriggerRequest
{
    public Workflow Workflow { get; set; } = null!;
    public string ActivityId { get; set; } = null!;
    public string? CorrelationId { get; set; }
    public IDictionary<string, object>? Input { get; set; }
    public IDictionary<string, object>? Variables { get; set; }
    public IDictionary<string, object>? Properties { get; set; }
    public string? ParentWorkflowInstanceId { get; set; }
    
}