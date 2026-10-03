using System.Text.Json.Nodes;
using Crest.Workflows.Api.Client.Resources.Resilience.Contracts;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Services;

/// <inheritdoc />
public class RemoteResilienceStrategyCatalog(IBackendApiClientProvider backendApiClientProvider, IRemoteFeatureProvider remoteFeatureProvider) : IResilienceStrategyCatalog
{
    private bool? _isResilienceEnabled;

    /// <inheritdoc />
    public async ValueTask<IEnumerable<JsonObject>> ListAsync(string category, CancellationToken cancellationToken = default)
    {
        // Check if the Resilience feature is enabled before making API calls.
        // Cache the result for the lifetime of this scoped service to avoid repeated backend requests.
        _isResilienceEnabled ??= await remoteFeatureProvider.IsEnabledAsync("Crest.Workflows.Resilience", cancellationToken);

        if (!_isResilienceEnabled.Value)
            return [];

        var api = await backendApiClientProvider.GetApiAsync<IResilienceStrategiesApi>(cancellationToken);
        var response = await api.ListAsync(category, cancellationToken);

        return response.Items;
    }
}