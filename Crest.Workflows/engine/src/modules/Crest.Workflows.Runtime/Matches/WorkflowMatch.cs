using Crest.Workflows.Management.Entities;

namespace Crest.Workflows.Runtime.Matches;

public record WorkflowMatch(string? CorrelationId, object? Payload);