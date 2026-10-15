using Grove.Workflows.Api.Client.Resources.IncidentStrategies.Contracts;
using Grove.Workflows.Api.Client.Resources.IncidentStrategies.Models;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Services;

/// <summary>
/// Provides incident strategies from a remote server.
/// </summary>
public class RemoteIncidentStrategiesProvider(IBackendApiClientProvider backendApiClientProvider) : IIncidentStrategiesProvider
{
    /// <inheritdoc />
    public async ValueTask<IEnumerable<IncidentStrategyDescriptor>> GetIncidentStrategiesAsync(CancellationToken cancellationToken = default)
    {
        var api = await backendApiClientProvider.GetApiAsync<IIncidentStrategiesApi>(cancellationToken);
        var response = await api.ListAsync(cancellationToken);

        return response.Items;
    }
}