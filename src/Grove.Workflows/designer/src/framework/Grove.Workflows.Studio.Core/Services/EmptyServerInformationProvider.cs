using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Models;

namespace Grove.Workflows.Studio.Services;

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