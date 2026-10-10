using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;

namespace Crest.Workflows.Studio.Services;

/// <summary>
/// A default implementation of <see cref="IServerInformationProvider"/> that returns dummy data.
/// </summary>
public class EmptyServerInformationProvider : IServerInformationProvider
{
    /// <inheritdoc />
    public ValueTask<ServerInformation> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        return new (new ServerInformation("0.0.0"));
    }
}