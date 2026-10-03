using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Responses;

namespace Crest.Workflows.Runtime;

/// <summary>
/// Posts a message to a topic to cancel a specified set of workflows.
/// </summary>
public interface IWorkflowCancellationDispatcher
{
    /// <summary>
    /// Cancels the specified workflow instances.
    /// </summary>
    Task<DispatchCancelWorkflowsResponse> DispatchAsync(DispatchCancelWorkflowRequest request, CancellationToken cancellationToken = default);
}