using Grove.Workflows.Runtime.Requests;
using Grove.Workflows.Runtime.Responses;

namespace Grove.Workflows.Runtime;

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