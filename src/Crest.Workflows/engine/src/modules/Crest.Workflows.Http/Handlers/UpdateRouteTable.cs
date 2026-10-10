using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Notifications;
using JetBrains.Annotations;

namespace Crest.Workflows.Http.Handlers;

/// <summary>
/// A handler that updates the route table when workflow triggers and bookmarks are indexed.
/// </summary>
[UsedImplicitly]
public class UpdateRouteTable(IRouteTableUpdater routeTableUpdater) :
    INotificationHandler<WorkflowTriggersIndexed>,
    INotificationHandler<WorkflowBookmarksIndexed>
{
    /// <inheritdoc />
    public async Task HandleAsync(WorkflowTriggersIndexed notification, CancellationToken cancellationToken)
    {
        routeTableUpdater.RemoveRoutes(notification.IndexedWorkflowTriggers.RemovedTriggers);
        await routeTableUpdater.AddRoutesAsync(notification.IndexedWorkflowTriggers.AddedTriggers, cancellationToken);
        await routeTableUpdater.AddRoutesAsync(notification.IndexedWorkflowTriggers.UnchangedTriggers, cancellationToken);
    }

    /// <inheritdoc />
    public async Task HandleAsync(WorkflowBookmarksIndexed notification, CancellationToken cancellationToken)
    {
        routeTableUpdater.RemoveRoutes(notification.IndexedWorkflowBookmarks.RemovedBookmarks);
        await routeTableUpdater.AddRoutesAsync(notification.IndexedWorkflowBookmarks.AddedBookmarks, notification.IndexedWorkflowBookmarks.WorkflowExecutionContext, cancellationToken);
    }
}