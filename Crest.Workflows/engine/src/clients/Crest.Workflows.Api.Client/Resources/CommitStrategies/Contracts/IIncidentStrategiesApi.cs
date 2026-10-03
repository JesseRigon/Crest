using Crest.Workflows.Api.Client.Resources.CommitStrategies.Models;
using Crest.Workflows.Api.Client.Shared.Models;
using Refit;

namespace Crest.Workflows.Api.Client.Resources.CommitStrategies.Contracts;

/// <summary>
/// Represents a client for the commit strategies API.
/// </summary>
public interface ICommitStrategiesApi
{
    /// <summary>
    /// Lists workflow commit strategies.
    /// </summary>
    /// <returns>A list response containing activity commit strategy descriptors and their count.</returns>
    [Get("/descriptors/commit-strategies/workflows")]
    Task<ListResponse<CommitStrategyDescriptor>> ListWorkflowStrategiesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists activity commit strategies.
    /// </summary>
    /// <returns>A list response containing activity commit strategy descriptors and their count.</returns>
    [Get("/descriptors/commit-strategies/activities")]
    Task<ListResponse<CommitStrategyDescriptor>> ListActivityStrategiesAsync(CancellationToken cancellationToken = default);
}