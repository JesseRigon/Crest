using Crest.Workflows.Api.Client.Resources.LogPersistenceStrategies;

namespace Crest.Workflows.Studio.Workflows.Domain.Contracts;

/// <summary>
/// A services that provides log persistence strategies.
/// </summary>
public interface ILogPersistenceStrategyService
{
    /// <summary>
    /// Gets the log persistence strategies.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IEnumerable<LogPersistenceStrategyDescriptor>> GetLogPersistenceStrategiesAsync(CancellationToken cancellationToken = default);
}