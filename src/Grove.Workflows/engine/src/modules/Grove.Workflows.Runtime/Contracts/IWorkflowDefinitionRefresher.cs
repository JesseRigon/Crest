using Grove.Workflows.Runtime.Requests;
using Grove.Workflows.Runtime.Responses;

namespace Grove.Workflows.Runtime;

/// <summary>
/// Refreshes all workflows by re-indexing their triggers.
/// </summary>
public interface IWorkflowDefinitionsRefresher
{
    /// <summary>
    /// Refreshes all workflows by re-indexing their triggers.
    /// </summary>
    Task<RefreshWorkflowDefinitionsResponse> RefreshWorkflowDefinitionsAsync(RefreshWorkflowDefinitionsRequest request, CancellationToken cancellationToken = default);
}