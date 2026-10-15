using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Runtime.Entities;

namespace Grove.Workflows.Runtime.Notifications;

/// <summary>
/// Triggered when a bookmark is being saved.
/// </summary>
/// <param name="Bookmark">The bookmark being saved.</param>
public record BookmarkSaving(StoredBookmark Bookmark) : INotification;