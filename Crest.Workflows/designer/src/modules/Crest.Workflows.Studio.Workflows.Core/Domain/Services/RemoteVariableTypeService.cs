using Crest.Workflows.Api.Client.Resources.VariableTypes.Contracts;
using Crest.Workflows.Api.Client.Resources.VariableTypes.Models;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Services;

/// <summary>
/// A variable type service that uses a remote backend to retrieve variable types.
/// </summary>
public class RemoteVariableTypeService : IVariableTypeService
{
    private readonly IBackendApiClientProvider _backendApiClientProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoteVariableTypeService"/> class.
    /// </summary>
    public RemoteVariableTypeService(IBackendApiClientProvider backendApiClientProvider)
    {
        _backendApiClientProvider = backendApiClientProvider;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<VariableTypeDescriptor>> GetVariableTypesAsync(CancellationToken cancellationToken = default)
    {
        var api = await _backendApiClientProvider.GetApiAsync<IVariableTypesApi>(cancellationToken);
        var response = await api.ListAsync(cancellationToken);
        return response.Items;
    }
}