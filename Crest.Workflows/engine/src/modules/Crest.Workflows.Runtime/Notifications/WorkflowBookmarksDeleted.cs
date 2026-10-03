using Crest.Workflows.Mediator.Contracts;
using Crest.Workflows.Models;

namespace Crest.Workflows.Runtime.Notifications;

public record WorkflowBookmarksDeleted(List<Bookmark> Bookmarks) : INotification;