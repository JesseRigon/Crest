using Grove.Workflows.Common.Models;
using Grove.Workflows.Resilience.Entities;

namespace Grove.Workflows.Resilience;

public interface IRetryAttemptReader
{
    Task<Page<RetryAttemptRecord>> ReadAttemptsAsync(string activityInstanceId, PageArgs? pageArgs = null, CancellationToken cancellationToken = default);   
}