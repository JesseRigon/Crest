using Crest.Workflows.Api.Client.Resources.WorkflowActivationStrategies.Contracts;
using Crest.Workflows.Api.Client.Resources.WorkflowActivationStrategies.Models;
using Crest.Workflows.Studio.Contracts;
using Crest.Workflows.Studio.Workflows.Domain.Contracts;

namespace Crest.Workflows.Studio.Workflows.Domain.Services;

/// <summary>
/// A workflow activation strategy service that uses a remote backend to retrieve workflow activation strategies.
/// </summary>
public class RemoteWorkflowActivationStrategyService(IBackendApiClientProvider backendApiClientProvider) : IWorkflowActivationStrategyService
{
    /// <inheritdoc />
    public async Task<IEnumerable<WorkflowActivationStrategyDescriptor>> GetWorkflowActivationStrategiesAsync(CancellationToken cancellationToken = default)
    {
        var api = await backendApiClientProvider.GetApiAsync<IWorkflowActivationStrategiesApi>(cancellationToken);
        var response = await api.ListAsync(cancellationToken);
        return response.Items;
    }
}