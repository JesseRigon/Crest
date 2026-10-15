using Grove.Workflows.Management.Entities;

namespace Grove.Workflows.Runtime.Matches;

public record WorkflowMatch(string? CorrelationId, object? Payload);