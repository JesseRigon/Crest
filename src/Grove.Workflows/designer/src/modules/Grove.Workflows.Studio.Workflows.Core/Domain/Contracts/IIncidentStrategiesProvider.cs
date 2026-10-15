using Grove.Workflows.Api.Client.Resources.IncidentStrategies.Models;

namespace Grove.Workflows.Studio.Workflows.Domain.Contracts;

/// <summary>
/// Provides incident strategies.
/// </summary>
public interface IIncidentStrategiesProvider
{
    /// <summary>
    /// Gets incident strategies.
    /// </summary>
    ValueTask<IEnumerable<IncidentStrategyDescriptor>> GetIncidentStrategiesAsync(CancellationToken cancellationToken = default);
}