using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Runtime.Entities;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// Triggered when bookmarks are being deleted.
/// </summary>
/// <param name="Bookmarks">The bookmarks being deleted.</param>
public record BookmarksDeleting(ICollection<StoredBookmark> Bookmarks) : INotification;