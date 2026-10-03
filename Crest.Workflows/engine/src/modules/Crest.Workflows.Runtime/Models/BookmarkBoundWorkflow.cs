using Crest.Workflows.Runtime.Entities;

namespace Crest.Workflows.Runtime;

/// <summary>
/// Represents a workflow bound to one or more bookmarks.
/// </summary>
public record BookmarkBoundWorkflow(string WorkflowInstanceId, ICollection<StoredBookmark> Bookmarks);