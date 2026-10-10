using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Runtime.Entities;

namespace Crest.Workflows.Runtime.Notifications;

/// <summary>
/// Triggered when a bookmark is being saved.
/// </summary>
/// <param name="Bookmark">The bookmark being saved.</param>
public record BookmarkSaving(StoredBookmark Bookmark) : INotification;