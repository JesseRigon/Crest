using Grove.Workflows.Api.Client.Resources.LogPersistenceStrategies;
using Grove.Workflows.Studio.Contracts;
using Grove.Workflows.Studio.Workflows.Domain.Contracts;

namespace Grove.Workflows.Studio.Workflows.Domain.Services;

/// <inheritdoc />
public class RemoteLogPersistenceStrategyService(IBackendApiClientProvider backendApiClientProvider) : ILogPersistenceStrategyService
{
    private ICollection<LogPersistenceStrategyDescriptor>? _descriptors;
    
    /// <inheritdoc />
    public async Task<IEnumerable<LogPersistenceStrategyDescriptor>> GetLogPersistenceStrategiesAsync(CancellationToken cancellationToken = default)
    {
        if (_descriptors == null)
        {
            var api = await backendApiClientProvider.GetApiAsync<ILogPersistenceStrategiesApi>(cancellationToken);
            var response = await api.ListAsync(cancellationToken);
            _descriptors = response.Items;
        }

        return _descriptors;
    }
}