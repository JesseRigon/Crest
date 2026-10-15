using Grove.Workflows.Resilience.Entities;
using Grove.Workflows;

namespace Grove.Workflows.Resilience.Models;

public record RecordRetryAttemptsContext(ActivityExecutionContext ActivityExecutionContext, ICollection<RetryAttemptRecord> Attempts, CancellationToken CancellationToken);