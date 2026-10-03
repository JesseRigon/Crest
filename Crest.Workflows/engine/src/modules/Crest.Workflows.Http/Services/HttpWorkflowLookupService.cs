using Crest.Workflows.Management;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Models;
using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Open.Linq.AsyncExtensions;

namespace Crest.Workflows.Http.Services;

/// <inheritdoc />
public class HttpWorkflowLookupService(ITriggerStore triggerStore, IWorkflowDefinitionService workflowDefinitionService) : IHttpWorkflowLookupService
{
    /// <inheritdoc />
    public async Task<HttpWorkflowLookupResult?> FindWorkflowAsync(string bookmarkHash, CancellationToken cancellationToken = default)
    {
        var triggers = await FindTriggersAsync(bookmarkHash, cancellationToken).ToList();

        if (triggers.Count > 1)
            return new(null, triggers);

        var trigger = triggers.SingleOrDefault();

        if (trigger == null)
            return default;

        var workflowGraph = await FindWorkflowGraphAsync(trigger, cancellationToken);

        if (workflowGraph == null)
            return default;

        return new(workflowGraph, triggers);
    }

    private async Task<IEnumerable<StoredTrigger>> FindTriggersAsync(string bookmarkHash, CancellationToken cancellationToken)
    {
        var triggerFilter = new TriggerFilter
        {
            Hash = bookmarkHash
        };
        return await triggerStore.FindManyAsync(triggerFilter, cancellationToken);
    }

    private async Task<WorkflowGraph?> FindWorkflowGraphAsync(StoredTrigger trigger, CancellationToken cancellationToken)
    {
        var workflowDefinitionVersionId = trigger.WorkflowDefinitionVersionId;
        var filter = new WorkflowDefinitionFilter
        {
            Id = workflowDefinitionVersionId
        };
        return await workflowDefinitionService.FindWorkflowGraphAsync(filter, cancellationToken);
    }
}