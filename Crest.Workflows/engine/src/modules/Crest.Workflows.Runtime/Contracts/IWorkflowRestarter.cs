using Crest.Workflows.Runtime.Tasks;

namespace Crest.Workflows.Runtime;

/// <summary>
/// Defines the contract for restarting workflows in the runtime environment.
/// </summary>
/// <remarks>
/// This service is used by the <see cref="RestartInterruptedWorkflowsTask"/> responsible for restarting interrupted workflows.
/// </remarks>
public interface IWorkflowRestarter
{
    /// <summary>
    /// Restarts a workflow with the specified workflow instance ID.
    /// </summary>
    /// <param name="workflowInstanceId">The ID of the workflow instance to restart.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public Task RestartWorkflowAsync(string workflowInstanceId, CancellationToken cancellationToken = default);
}