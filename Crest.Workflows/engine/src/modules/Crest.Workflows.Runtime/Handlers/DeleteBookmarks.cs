using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Management.Notifications;
using Crest.Workflows.Runtime.Filters;
using JetBrains.Annotations;

namespace Crest.Workflows.Runtime.Handlers;

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