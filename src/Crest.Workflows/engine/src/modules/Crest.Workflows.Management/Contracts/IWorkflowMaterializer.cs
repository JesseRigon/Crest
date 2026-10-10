using Crest.Workflows.Activities;
using Crest.Workflows.Management.Entities;

namespace Crest.Workflows.Management;

/// <summary>
/// A service that can materialize a workflow from a workflow definition.
/// </summary>
public interface IWorkflowMaterializer
{
    /// <summary>
    /// The name of the materializer.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Materializes a workflow from a workflow definition.
    /// </summary>
    /// <param name="definition">The workflow definition.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The materialized workflow.</returns>
    ValueTask<Workflow> MaterializeAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default);
}