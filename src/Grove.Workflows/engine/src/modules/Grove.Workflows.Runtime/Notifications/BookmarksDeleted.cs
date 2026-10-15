using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Runtime.Entities;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// Triggered when bookmarks have been deleted.
/// </summary>
/// <param name="Bookmarks">The bookmarks that have been deleted.</param>
public record BookmarksDeleted(ICollection<StoredBookmark> Bookmarks) : INotification;