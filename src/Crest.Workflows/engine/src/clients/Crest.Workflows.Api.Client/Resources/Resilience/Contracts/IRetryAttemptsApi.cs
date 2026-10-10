using Crest.Workflows.Api.Client.Resources.Resilience.Models;
using Crest.Workflows.Api.Client.Shared.Models;
using Refit;

namespace Crest.Workflows.Api.Client.Resources.Resilience.Contracts;

public interface IRetryAttemptsApi
{
    [Get("/resilience/retries/{activityInstanceId}")]
    Task<PagedListResponse<RetryAttemptRecord>> ListAsync(string activityInstanceId, int? skip = null, int? take = null, CancellationToken cancellationToken = default);
}