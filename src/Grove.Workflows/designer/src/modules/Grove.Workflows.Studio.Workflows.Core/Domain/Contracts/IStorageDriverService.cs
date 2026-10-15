using Grove.Workflows.Api.Client.Resources.StorageDrivers.Models;

namespace Grove.Workflows.Studio.Workflows.Domain.Contracts;

/// <summary>
/// A services that provides storage drivers.
/// </summary>
public interface IStorageDriverService
{
    /// <summary>
    /// Gets the storage drivers.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<IEnumerable<StorageDriverDescriptor>> GetStorageDriversAsync(CancellationToken cancellationToken = default);
}