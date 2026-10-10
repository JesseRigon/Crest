using Crest.Workflows.Extensions;
using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Middleware.Activities;
using Crest.Workflows.Runtime.Notifications;
using Crest.Workflows.Runtime.Stimuli;
using JetBrains.Annotations;

namespace Crest.Workflows.Runtime.Handlers;

/// <summary>
/// A handler that cancels background activities based on removed bookmarks.
/// </summary>
[UsedImplicitly]
public class CancelBackgroundActivities(IBackgroundActivityScheduler backgroundActivityScheduler) : INotificationHandler<WorkflowBookmarksIndexed>
{
    /// <inheritdoc />
    public async Task HandleAsync(WorkflowBookmarksIndexed notification, CancellationToken cancellationToken)
    {
        var removedBookmarks = notification.IndexedWorkflowBookmarks.RemovedBookmarks.Where(x => x.Name == BackgroundActivityInvokerMiddleware.BackgroundActivityBookmarkName);

        foreach (var removedBookmark in removedBookmarks)
        {
            var payload = removedBookmark.GetPayload<BackgroundActivityStimulus>();
            if (payload.JobId != null)
                await backgroundActivityScheduler.UnscheduledAsync(payload.JobId, cancellationToken);
        }
    }
}