using Crest.Workflows.Common.Models;
using Crest.Workflows.Resilience.Entities;

namespace Crest.Workflows.Resilience;

public interface IRetryAttemptReader
{
    Task<Page<RetryAttemptRecord>> ReadAttemptsAsync(string activityInstanceId, PageArgs? pageArgs = null, CancellationToken cancellationToken = default);   
}