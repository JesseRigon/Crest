using Grove.Workflows.Api.Client.Resources.StorageDrivers.Contracts;
using Grove.Workflows.Api.Client.Resources.StorageDrivers.Models;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Services;

/// <summary>
/// A storage driver service that uses a remote backend to retrieve storage drivers.
/// </summary>
public class RemoteStorageDriverService : IStorageDriverService
{
    private readonly IBackendApiClientProvider _backendApiClientProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteStorageDriverService"/> class.
    /// </summary>
    public RemoteStorageDriverService(IBackendApiClientProvider backendApiClientProvider)
    {
        _backendApiClientProvider = backendApiClientProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<StorageDriverDescriptor>> GetStorageDriversAsync(CancellationToken cancellationToken = default)
    {
        var api = await _backendApiClientProvider.GetApiAsync<IStorageDriversApi>(cancellationToken);
        var response = await api.ListAsync(cancellationToken);
        
        return response.Items;
    }
}