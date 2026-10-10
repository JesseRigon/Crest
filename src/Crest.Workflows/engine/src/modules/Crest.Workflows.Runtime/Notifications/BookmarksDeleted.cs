using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Entities;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// Triggered when bookmarks have been deleted.
/// </summary>
/// <param name="Bookmarks">The bookmarks that have been deleted.</param>
public record BookmarksDeleted(ICollection<StoredBookmark> Bookmarks) : INotification;