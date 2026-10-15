using Grove.Workflows.Api.Client.Resources.WorkflowActivationStrategies.Models;

namespace Grove.Workflows.Studio.Workflows.Domain.Contracts;

/// <summary>
/// A services that provides workflow activation strategies.
/// </summary>
public interface IWorkflowActivationStrategyService
{
    /// <summary>
    /// Gets the workflow activation strategies.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<IEnumerable<WorkflowActivationStrategyDescriptor>> GetWorkflowActivationStrategiesAsync(CancellationToken cancellationToken = default);
}