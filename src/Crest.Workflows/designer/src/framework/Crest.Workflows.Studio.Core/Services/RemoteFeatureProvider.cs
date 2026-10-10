using System.Net;
using Crest.Workflows.Api.Client.Resources.Features.Contracts;
using Crest.Workflows.Api.Client.Resources.Features.Models;
using Crest.Workflows.Studio.Contracts;
using Refit;

namespace Crest.Workflows.Studio.Services;

/// <summary>
/// A feature service that uses a remote backend to retrieve feature flags.
/// </summary>
public class RemoteFeatureProvider(IBackendApiClientProvider remoteBackendApiClientProvider) : IRemoteFeatureProvider
{
    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(string featureName, CancellationToken cancellationToken = default)
    {
        var api = await remoteBackendApiClientProvider.GetApiAsync<IFeaturesApi>(cancellationToken);

        try
        {
            _ = await api.GetAsync(featureName, cancellationToken);
            return true;
        }
        catch (ApiException e) when(e.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<FeatureDescriptor>> ListAsync(CancellationToken cancellationToken = default)
    {
        var api = await remoteBackendApiClientProvider.GetApiAsync<IFeaturesApi>(cancellationToken);
        var response = await api.ListAsync(cancellationToken);
        return response.Items;
    }
}