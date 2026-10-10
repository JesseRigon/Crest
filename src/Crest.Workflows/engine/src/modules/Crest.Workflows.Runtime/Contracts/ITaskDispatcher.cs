using Crest.Workflows.Runtime.Notifications;

namespace Crest.Workflows.Runtime;

/// <summary>
/// Dispatches a request for running a task.
/// </summary>
public interface ITaskDispatcher
{
    /// <summary>
    /// Asynchronously publishes the specified event using the workflow dispatcher.
    /// </summary>
    Task DispatchAsync(RunTaskRequest request, CancellationToken cancellationToken = default);
}