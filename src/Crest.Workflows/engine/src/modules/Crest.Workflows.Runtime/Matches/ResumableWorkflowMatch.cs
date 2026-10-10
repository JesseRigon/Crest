using Crest.Workflows.Management.Entities;

namespace Crest.Workflows.Runtime.Matches;

public record ResumableWorkflowMatch(string WorkflowInstanceId, string? CorrelationId, string? BookmarkId, object? Payload)
    : WorkflowMatch(CorrelationId, Payload);