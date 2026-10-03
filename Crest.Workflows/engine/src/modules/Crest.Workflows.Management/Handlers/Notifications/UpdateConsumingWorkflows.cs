using Crest.Workflows.Extensions;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Notifications;

namespace Crest.Workflows.Management.Handlers.Notifications;

/// <summary>
/// Updates consuming workflows when a workflow definition is published.
/// </summary>
public class UpdateConsumingWorkflows(IWorkflowReferenceUpdater workflowReferenceUpdater) : INotificationHandler<WorkflowDefinitionPublished>
{
    /// <inheritdoc />
    public async Task HandleAsync(WorkflowDefinitionPublished notification, CancellationToken cancellationToken)
    {
        var definition = notification.WorkflowDefinition;
        var result = await workflowReferenceUpdater.UpdateWorkflowReferencesAsync(definition, cancellationToken);
        notification.AffectedWorkflows.WorkflowDefinitions.AddRange(result.UpdatedWorkflows);
    }
}