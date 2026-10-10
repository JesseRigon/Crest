using Crest.Workflows.Api.Client.Resources.IncidentStrategies.Models;
using Crest.Workflows.Api.Client.Shared.Models;
using Refit;

namespace Crest.Workflows.Api.Client.Resources.IncidentStrategies.Contracts;

/// <summary>
/// Represents a client for the variable types API.
/// </summary>
public interface IIncidentStrategiesApi
{
    /// <summary>
    /// Lists incident strategies.
    /// </summary>
    [Get("/descriptors/incident-strategies")]
    Task<ListResponse<IncidentStrategyDescriptor>> ListAsync(CancellationToken cancellationToken = default);
}