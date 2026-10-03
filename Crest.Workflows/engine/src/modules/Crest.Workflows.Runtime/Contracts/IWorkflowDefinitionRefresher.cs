using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Responses;

namespace Crest.Workflows.Runtime;

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