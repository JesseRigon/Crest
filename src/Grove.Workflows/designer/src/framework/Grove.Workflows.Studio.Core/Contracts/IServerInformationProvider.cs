using Grove.Workflows.Studio.Models;

namespace Grove.Workflows.Studio.Contracts;

/// <summary>
/// Provides information about the server.
/// </summary>
public interface IServerInformationProvider
{
    /// <summary>
    /// Gets information about the server.
    /// </summary>
    ValueTask<ServerInformation> GetInfoAsync(CancellationToken cancellationToken = default);
}