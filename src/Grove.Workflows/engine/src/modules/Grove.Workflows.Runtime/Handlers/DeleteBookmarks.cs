using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Management.Notifications;
using Grove.Workflows.Runtime.Filters;
using JetBrains.Annotations;

namespace Grove.Workflows.Runtime.Handlers;

/// <summary>
/// Deletes bookmarks for workflow instances being deleted.
/// </summary>
[UsedImplicitly]
public class DeleteBookmarks(IBookmarkManager bookmarkManager) : INotificationHandler<WorkflowInstancesDeleting>
{
    /// <inheritdoc />
    public async Task HandleAsync(WorkflowInstancesDeleting notification, CancellationToken cancellationToken)
    {
        await bookmarkManager.DeleteManyAsync(new()
        {
            WorkflowInstanceIds = notification.Ids
        }, cancellationToken);
    }
}