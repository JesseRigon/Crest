using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Models;

namespace Crest.Workflows.Management;

/// <summary>
/// Updates references to the specified workflow of all workflows that reference it.
/// </summary>
public interface IWorkflowReferenceUpdater
{
    /// <summary>
    /// Updates references to the specified workflow of all workflows that reference it.
    /// </summary>
    /// <param name="referencedDefinition">The workflow definition that is being referenced. All workflows that reference this definition will be updated to use this newest version.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    Task<UpdateWorkflowReferencesResult> UpdateWorkflowReferencesAsync(WorkflowDefinition referencedDefinition, CancellationToken cancellationToken = default);
}