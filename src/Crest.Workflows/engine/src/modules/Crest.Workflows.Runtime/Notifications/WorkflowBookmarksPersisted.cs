using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Helpers;
using Crest.Workflows.Models;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// Published when the bookmarks of a workflow instance have been persisted.
/// </summary>
/// <param name="Diff">The bookmarks that were added, removed, or unchanged.</param>
public record WorkflowBookmarksPersisted(Diff<Bookmark> Diff) : INotification;