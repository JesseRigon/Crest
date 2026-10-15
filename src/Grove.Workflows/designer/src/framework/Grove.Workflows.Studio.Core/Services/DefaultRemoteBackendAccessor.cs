using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Models;
using Grove.Workflows.Studio.Options;
using Microsoft.Extensions.Options;

namespace Grove.Workflows.Studio.Services;

/// <summary>
/// A default implementation of <see cref="IRemoteBackendAccessor"/> that uses the <see cref="BackendOptions"/> to determine the URL of the remote backend.
/// </summary>
public class DefaultRemoteBackendAccessor : IRemoteBackendAccessor
{
    private readonly IOptions<BackendOptions> _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRemoteBackendAccessor"/> class.
    /// </summary>
    public DefaultRemoteBackendAccessor(IOptions<BackendOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc />
    public RemoteBackend RemoteBackend => new(_options.Value.Url);
}