using System.Text.Json.Nodes;
using Grove.Workflows.Api.Client.Resources.Resilience.Contracts;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Services;

/// <inheritdoc />
public class RemoteResilienceStrategyCatalog(IBackendApiClientProvider backendApiClientProvider, IRemoteFeatureProvider remoteFeatureProvider) : IResilienceStrategyCatalog
{
    private bool? _isResilienceEnabled;

    /// <inheritdoc />
    public async ValueTask<IEnumerable<JsonObject>> ListAsync(string category, CancellationToken cancellationToken = default)
    {
        // Check if the Resilience feature is enabled before making API calls.
        // Cache the result for the lifetime of this scoped service to avoid repeated backend requests.
        _isResilienceEnabled ??= await remoteFeatureProvider.IsEnabledAsync("Grove.Workflows.Resilience", cancellationToken);

        if (!_isResilienceEnabled.Value)
            return [];

        var api = await backendApiClientProvider.GetApiAsync<IResilienceStrategiesApi>(cancellationToken);
        var response = await api.ListAsync(category, cancellationToken);

        return response.Items;
    }
}