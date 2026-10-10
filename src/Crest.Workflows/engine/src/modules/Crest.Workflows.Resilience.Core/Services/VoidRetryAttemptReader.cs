using Crest.Workflows.Common.Models;
using Crest.Workflows.Resilience.Entities;

namespace Crest.Workflows.Resilience;

public class VoidRetryAttemptReader : IRetryAttemptReader
{
    public static VoidRetryAttemptReader Instance { get; } = new();
    
    public Task<Page<RetryAttemptRecord>> ReadAttemptsAsync(string activityInstanceId, PageArgs? pageArgs = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Page.Empty<RetryAttemptRecord>());
    }
}