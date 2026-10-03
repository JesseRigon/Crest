using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.Notifications;
using Microsoft.Extensions.Logging;

namespace Crest.Workflows.Runtime;

/// <summary>
/// Default implementation of <see cref="IBookmarkManager"/>.
/// </summary>
public class DefaultBookmarkManager(IBookmarkStore bookmarkStore, INotificationSender notificationSender, ILogger<DefaultBookmarkManager> logger)
    : IBookmarkManager
{
    /// <inheritdoc />
    public async Task<long> DeleteManyAsync(BookmarkFilter filter, CancellationToken cancellationToken = default)
    {
        var bookmarks = (await bookmarkStore.FindManyAsync(filter, cancellationToken)).ToList();
        await notificationSender.SendAsync(new BookmarksDeleting(bookmarks), cancellationToken);
        var count = await bookmarkStore.DeleteAsync(filter, cancellationToken);
        await notificationSender.SendAsync(new BookmarksDeleted(bookmarks), cancellationToken);

        if (logger.IsEnabled(LogLevel.Debug))
        {
            foreach (var bookmark in bookmarks)
                logger.LogDebug("Deleted bookmark {BookmarkId} for workflow {WorkflowInstanceId}", bookmark.Id, bookmark.WorkflowInstanceId);
        }

        return count;
    }

    /// <inheritdoc />
    public async Task SaveAsync(StoredBookmark bookmark, CancellationToken cancellationToken = default)
    {
        await notificationSender.SendAsync(new BookmarkSaving(bookmark), cancellationToken);
        await bookmarkStore.SaveAsync(bookmark, cancellationToken);
        await notificationSender.SendAsync(new BookmarkSaved(bookmark), cancellationToken);
    }
}