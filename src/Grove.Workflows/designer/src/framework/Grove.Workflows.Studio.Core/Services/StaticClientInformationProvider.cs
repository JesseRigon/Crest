using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Models;
using JetBrains.Annotations;

namespace Grove.Workflows.Studio.Services;

/// <summary>
/// Provides static client information by returning <see cref="ToolVersion.Version"/>.
/// </summary>
[UsedImplicitly]
public class StaticClientInformationProvider : IClientInformationProvider
{
    /// <inheritdoc />
    public ValueTask<ClientInformation> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var version = ToolVersion.Version.ToString();
        return new(new ClientInformation(version));
    }
}