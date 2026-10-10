using Crest.Workflows.Api.Client.Resources.WorkflowDefinitions.Models;
using Crest.Workflows.Studio.Models;
using Crest.Workflows.Studio.Workflows.Domain.Models;

namespace Crest.Workflows.Studio.Workflows.Domain.Contracts;

/// <summary>
/// A service that can be used to manage the history of workflow definitions.
/// </summary>
public interface IWorkflowDefinitionHistoryService
{
    /// <summary>
    /// Retracts a workflow definition.
    /// </summary>
    Task<Result<WorkflowDefinition, ValidationErrors>> RetractAsync(WorkflowDefinition workflowDefinition, Func<WorkflowDefinition, Task>? workflowRetractedCallback = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Reverts the specified workflow definition to the specified version.
    /// </summary>
    Task<WorkflowDefinitionSummary> RevertAsync(WorkflowDefinitionVersion workflowDefinitionVersion, CancellationToken cancellationToken = default);
}