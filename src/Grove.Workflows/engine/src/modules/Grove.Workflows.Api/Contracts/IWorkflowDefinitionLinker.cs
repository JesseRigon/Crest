using Grove.Workflows.Models;
using Grove.Workflows.Api.Models;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Management.Models;

namespace Grove.Workflows.Api;

/// <summary>
/// Maps workflow definition models to liked models
/// </summary>
public interface IWorkflowDefinitionLinker
{
    /// <summary>
    /// Maps to an enhanced model that contains links with the possible operations applicable to a workflow definition.
    /// </summary>
    Task<LinkedWorkflowDefinitionModel> MapAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps a paged list to an enhanced model that contains links with the possible operations applicable to a workflow definition.
    /// </summary>
    PagedListResponse<LinkedWorkflowDefinitionSummary> MapAsync(PagedListResponse<WorkflowDefinitionSummary> list, CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps a list to an enhanced model that contains links with the possible operations applicable to a workflow definition.
    /// </summary>
    Task<List<LinkedWorkflowDefinitionModel>> MapAsync(List<WorkflowDefinition> definitions, CancellationToken cancellationToken = default);
}