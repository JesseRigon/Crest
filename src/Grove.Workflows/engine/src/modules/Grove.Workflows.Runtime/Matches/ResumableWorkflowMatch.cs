using Grove.Workflows.Management.Entities;

namespace Grove.Workflows.Runtime.Matches;

public record ResumableWorkflowMatch(string WorkflowInstanceId, string? CorrelationId, string? BookmarkId, object? Payload)
    : WorkflowMatch(CorrelationId, Payload);