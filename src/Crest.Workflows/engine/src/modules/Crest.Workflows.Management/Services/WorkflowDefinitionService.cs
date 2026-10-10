using Crest.Workflows.Common.Models;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Models;

namespace Crest.Workflows.Management.Services;

/// <inheritdoc />
public class WorkflowDefinitionService(
    IWorkflowDefinitionStore workflowDefinitionStore,
    IWorkflowGraphBuilder workflowGraphBuilder,
    Func<IEnumerable<IWorkflowMaterializer>> materializers)
    : IWorkflowDefinitionService
{
    /// <inheritdoc />
    public async Task<WorkflowGraph> MaterializeWorkflowAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
    {
        var workflowMaterializers = materializers();
        var materializer = workflowMaterializers.FirstOrDefault(x => x.Name == definition.MaterializerName);

        if (materializer == null)
            throw new("Provider not found");

        var workflow = await materializer.MaterializeAsync(definition, cancellationToken);
        return await workflowGraphBuilder.BuildAsync(workflow, cancellationToken);
    }

    /// <inheritdoc />
    public Task<WorkflowDefinition?> FindWorkflowDefinitionAsync(string definitionId, VersionOptions versionOptions, CancellationToken cancellationToken = default)
    {
        var handle = WorkflowDefinitionHandle.ByDefinitionId(definitionId, versionOptions);
        return FindWorkflowDefinitionAsync(handle, cancellationToken);
    }

    /// <inheritdoc />
    public Task<WorkflowDefinition?> FindWorkflowDefinitionAsync(string definitionVersionId, CancellationToken cancellationToken = default)
    {
        var handle = WorkflowDefinitionHandle.ByDefinitionVersionId(definitionVersionId);
        return FindWorkflowDefinitionAsync(handle, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition?> FindWorkflowDefinitionAsync(WorkflowDefinitionHandle handle, CancellationToken cancellationToken = default)
    {
        var filter = handle.ToFilter();
        return await workflowDefinitionStore.FindAsync(filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowDefinition?> FindWorkflowDefinitionAsync(WorkflowDefinitionFilter filter, CancellationToken cancellationToken = default)
    {
        return await workflowDefinitionStore.FindAsync(filter, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowGraph?> FindWorkflowGraphAsync(string definitionId, VersionOptions versionOptions, CancellationToken cancellationToken = default)
    {
        var definition = await FindWorkflowDefinitionAsync(definitionId, versionOptions, cancellationToken);

        if (definition == null)
            return null;

        return await MaterializeWorkflowAsync(definition, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowGraph?> FindWorkflowGraphAsync(string definitionVersionId, CancellationToken cancellationToken = default)
    {
        var definition = await FindWorkflowDefinitionAsync(definitionVersionId, cancellationToken);

        if (definition == null)
            return null;

        return await MaterializeWorkflowAsync(definition, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowGraph?> FindWorkflowGraphAsync(WorkflowDefinitionHandle definitionHandle, CancellationToken cancellationToken = default)
    {
        var definition = await FindWorkflowDefinitionAsync(definitionHandle, cancellationToken);

        if (definition == null)
            return null;

        return await MaterializeWorkflowAsync(definition, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<WorkflowGraph?> FindWorkflowGraphAsync(WorkflowDefinitionFilter filter, CancellationToken cancellationToken = default)
    {
        var definition = await FindWorkflowDefinitionAsync(filter, cancellationToken);

        if (definition == null)
            return null;

        return await MaterializeWorkflowAsync(definition, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<WorkflowGraph>> FindWorkflowGraphsAsync(WorkflowDefinitionFilter filter, CancellationToken cancellationToken = default)
    {
        var workflowDefinitions = await workflowDefinitionStore.FindManyAsync(filter, cancellationToken);
        var workflowGraphs = new List<WorkflowGraph>();
        foreach (var workflowDefinition in workflowDefinitions)
        {
            var workflowGraph = await MaterializeWorkflowAsync(workflowDefinition, cancellationToken);
            workflowGraphs.Add(workflowGraph);
        }

        return workflowGraphs;
    }
}