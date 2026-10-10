using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Entities;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// Triggered when bookmarks are being deleted.
/// </summary>
/// <param name="Bookmarks">The bookmarks being deleted.</param>
public record BookmarksDeleting(ICollection<StoredBookmark> Bookmarks) : INotification;