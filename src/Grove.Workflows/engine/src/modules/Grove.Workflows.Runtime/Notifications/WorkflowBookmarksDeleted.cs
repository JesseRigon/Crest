using Grove.Workflows.Mediator.Contracts;
using Grove.Workflows.Models;

namespace Grove.Workflows.Runtime.Notifications;

public record WorkflowBookmarksDeleted(List<Bookmark> Bookmarks) : INotification;