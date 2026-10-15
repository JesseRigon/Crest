using Grove.Workflows.Api.Client.Resources.StorageDrivers.Models;

namespace Grove.Workflows.Api.Client.Resources.StorageDrivers.Responses;

/// <summary>
/// The response containing the storage drivers.
/// </summary>
/// <param name="Items">The storage drivers.</param>
public record ListStorageDriversResponse(ICollection<StorageDriverDescriptor> Items);