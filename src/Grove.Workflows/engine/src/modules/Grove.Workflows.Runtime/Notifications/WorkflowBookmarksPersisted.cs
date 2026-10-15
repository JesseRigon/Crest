using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Helpers;
using Grove.Workflows.Models;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// Published when the bookmarks of a workflow instance have been persisted.
/// </summary>
/// <param name="Diff">The bookmarks that were added, removed, or unchanged.</param>
public record WorkflowBookmarksPersisted(Diff<Bookmark> Diff) : INotification;