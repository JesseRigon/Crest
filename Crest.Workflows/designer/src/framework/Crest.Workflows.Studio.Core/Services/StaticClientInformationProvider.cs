using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;
using JetBrains.Annotations;

namespace Crest.Workflows.Studio.Services;

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