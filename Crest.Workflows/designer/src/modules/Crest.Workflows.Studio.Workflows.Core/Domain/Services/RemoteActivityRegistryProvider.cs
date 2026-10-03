using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Contracts;
using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Models;
using Crest.Workflows.Api.Client.Resources.ActivityDescriptors.Requests;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Services;

/// <summary>
/// An activity registry provider that uses a remote backend to retrieve activity descriptors.
/// </summary>
public class RemoteActivityRegistryProvider(IBackendApiClientProvider remoteBackendApiClientProvider) : IActivityRegistryProvider
{
    /// <inheritdoc />
    public async Task<IEnumerable<ActivityDescriptor>> ListAsync(CancellationToken cancellationToken = default)
    {
        var api = await remoteBackendApiClientProvider.GetApiAsync<IActivityDescriptorsApi>(cancellationToken);
        var request = new ListActivityDescriptorsRequest
        {
            Refresh = true
        };
        var response = await api.ListAsync(request, cancellationToken);
        
        return response.Items;
    }
}