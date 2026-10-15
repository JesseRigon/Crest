using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Management.Notifications;
using JetBrains.Annotations;

namespace Grove.Workflows.Runtime.Handlers;

[UsedImplicitly]
internal class IndexTriggers(ITriggerIndexer triggerIndexer) :
    INotificationHandler<WorkflowDefinitionPublished>,
    INotificationHandler<WorkflowDefinitionRetracted>
{
    public async Task HandleAsync(WorkflowDefinitionPublished notification, CancellationToken cancellationToken) => await triggerIndexer.IndexTriggersAsync(notification.WorkflowDefinition, cancellationToken);
    public async Task HandleAsync(WorkflowDefinitionRetracted notification, CancellationToken cancellationToken) => await triggerIndexer.IndexTriggersAsync(notification.WorkflowDefinition, cancellationToken);
}