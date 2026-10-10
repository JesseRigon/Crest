using Crest.Workflows.Models;
using Crest.Workflows.Api.Models;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Models;

namespace Crest.Workflows.Api;

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