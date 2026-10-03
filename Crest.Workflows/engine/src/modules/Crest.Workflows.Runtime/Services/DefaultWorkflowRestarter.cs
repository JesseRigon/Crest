using Crest.Workflows.Runtime.Requests;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Runtime;

/// <inheritdoc />
public class DefaultWorkflowRestarter(IWorkflowDispatcher workflowDispatcher, ILogger<DefaultWorkflowRestarter> logger) : IWorkflowRestarter
{
    /// <inheritdoc />
    public async Task RestartWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default)
    {
        var request = new DispatchWorkflowInstanceRequest(workflowInstanceId);
        var options = new DispatchWorkflowOptions();
        
        logger.LogInformation("Restarting workflow {WorkflowInstanceId}", workflowInstanceId);
        await workflowDispatcher.DispatchAsync(request, options, cancellationToken);
    }
}