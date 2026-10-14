using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Notifications;
using JetBrains.Annotations;

namespace Crest.Workflows.Http.Handlers;

/// <summary>
/// Tells the <see cref="IHttpEndpointRoutesObserver"/>s when an indexing added or removed an HTTP
/// endpoint trigger or bookmark.
/// </summary>
[UsedImplicitly]
public class NotifyHttpEndpointRoutesChanged(IEnumerable<IHttpEndpointRoutesObserver> observers) :
    INotificationHandler<WorkflowTriggersIndexed>,
    INotificationHandler<WorkflowBookmarksIndexed>
{
    public Task HandleAsync(WorkflowTriggersIndexed notification, CancellationToken cancellationToken)
    {
        var indexed = notification.IndexedWorkflowTriggers;
        if (indexed.AddedTriggers.Concat(indexed.RemovedTriggers).Any(trigger => trigger.Name == HttpStimulusNames.HttpEndpoint))
            Notify();

        return Task.CompletedTask;
    }

    public Task HandleAsync(WorkflowBookmarksIndexed notification, CancellationToken cancellationToken)
    {
        var indexed = notification.IndexedWorkflowBookmarks;
        if (indexed.AddedBookmarks.Concat(indexed.RemovedBookmarks).Any(bookmark => bookmark.Name == HttpStimulusNames.HttpEndpoint))
            Notify();

        return Task.CompletedTask;
    }

    private void Notify()
    {
        foreach (var observer in observers)
            observer.RoutesChanged();
    }
}
