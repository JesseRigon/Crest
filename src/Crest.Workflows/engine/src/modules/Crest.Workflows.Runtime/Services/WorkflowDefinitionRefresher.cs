using Crest.Workflows.Common.Entities;
using Crest.Workflows.Common.Models;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management;
using Crest.Workflows.Management.Entities;
using Crest.Workflows.Management.Filters;
using Crest.Workflows.Runtime.Notifications;
using Crest.Workflows.Runtime.Requests;
using Crest.Workflows.Runtime.Responses;

namespace Crest.Workflows.Runtime;

/// <inheritdoc />
public class WorkflowDefinitionsRefresher(IWorkflowDefinitionStore store, ITriggerIndexer triggerIndexer, INotificationSender notificationSender) : IWorkflowDefinitionsRefresher
{
    /// <inheritdoc />
    public async Task<RefreshWorkflowDefinitionsResponse> RefreshWorkflowDefinitionsAsync(RefreshWorkflowDefinitionsRequest request, CancellationToken cancellationToken)
    {
        var filter = new WorkflowDefinitionFilter
        {
            DefinitionIds = request.DefinitionIds,
            VersionOptions = VersionOptions.Published
        };

        var currentPage = 0;
        var processedWorkflowDefinitions = new List<WorkflowDefinition>();
        var batchSize = request.BatchSize;
        var order = new WorkflowDefinitionOrder<string>(x => x.Id, OrderDirection.Ascending);

        while (!cancellationToken.IsCancellationRequested)
        {
            var pageArgs = PageArgs.FromPage(currentPage, batchSize);
            var definitions = await store.FindManyAsync(filter, order, pageArgs, cancellationToken);

            if (definitions.Items.Count == 0)
                break;

            await IndexWorkflowTriggersAsync(definitions.Items, cancellationToken);
            processedWorkflowDefinitions.AddRange(definitions.Items);
            currentPage++;

            if (definitions.Items.Count < batchSize)
                break;
        }

        var processedWorkflowDefinitionIds = processedWorkflowDefinitions.Select(x => x.DefinitionId).ToList();
        var notification = new WorkflowDefinitionsRefreshed(processedWorkflowDefinitionIds);
        await notificationSender.SendAsync(notification, cancellationToken);
        return new(processedWorkflowDefinitionIds, request.DefinitionIds?.Except(processedWorkflowDefinitionIds)?.ToList() ?? []);
    }

    private async Task IndexWorkflowTriggersAsync(IEnumerable<WorkflowDefinition> definitions, CancellationToken cancellationToken)
    {
        foreach (var definition in definitions)
            await triggerIndexer.IndexTriggersAsync(definition, cancellationToken);
    }
}