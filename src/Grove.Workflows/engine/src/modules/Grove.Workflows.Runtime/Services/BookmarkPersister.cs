using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Runtime.Notifications;
using Grove.Workflows.Runtime.Requests;

namespace Grove.Workflows.Runtime;

/// <inheritdoc />
public class BookmarksPersister(IBookmarkUpdater bookmarkUpdater, INotificationSender notificationSender) : IBookmarksPersister
{
    /// <inheritdoc />
    public async Task PersistBookmarksAsync(UpdateBookmarksRequest updateBookmarksRequest)
    {
        await bookmarkUpdater.UpdateBookmarksAsync(updateBookmarksRequest);

        // Publish domain event.
        await notificationSender.SendAsync(new WorkflowBookmarksIndexed(new(
            updateBookmarksRequest.WorkflowExecutionContext,
            updateBookmarksRequest.Diff.Added, 
            updateBookmarksRequest.Diff.Removed, 
            updateBookmarksRequest.Diff.Unchanged)));

        // Publish domain event.
        await notificationSender.SendAsync(new WorkflowBookmarksPersisted(updateBookmarksRequest.Diff));
    }
}