using Crest.Workflows.Studio.Models;

namespace Crest.Workflows.Studio.Contracts;

/// <summary>
/// Provides information about the client.
/// </summary>
public interface IClientInformationProvider
{
    /// <summary>
    /// Gets information about the client.
    /// </summary>
    ValueTask<ClientInformation> GetInfoAsync(CancellationToken cancellationToken = default);
}