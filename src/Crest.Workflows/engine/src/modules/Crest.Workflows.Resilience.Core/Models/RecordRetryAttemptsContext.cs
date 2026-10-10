using Crest.Workflows.Resilience.Entities;
using Crest.Workflows;

namespace Crest.Workflows.Resilience.Models;

public record RecordRetryAttemptsContext(ActivityExecutionContext ActivityExecutionContext, ICollection<RetryAttemptRecord> Attempts, CancellationToken CancellationToken);