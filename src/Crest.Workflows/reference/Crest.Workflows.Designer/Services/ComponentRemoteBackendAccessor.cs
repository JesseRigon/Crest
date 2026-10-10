using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.Options;
using Crest.Workflows.Studio.Services;

namespace Crest.Workflows.Designer.Services;

/// <summary>
/// A default implementation of <see cref="IRemoteBackendAccessor"/> that uses the <see cref="BackendOptions"/> to determine the URL of the remote backend.
/// </summary>
public class ComponentRemoteBackendAccessor : IRemoteBackendAccessor
{
    private readonly BackendService _backendService;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRemoteBackendAccessor"/> class.
    /// </summary>
    public ComponentRemoteBackendAccessor(BackendService backendService)
    {
        _backendService = backendService;
    }

    /// <inheritdoc />
    public RemoteBackend RemoteBackend => new(new(_backendService.RemoteEndpoint));
}