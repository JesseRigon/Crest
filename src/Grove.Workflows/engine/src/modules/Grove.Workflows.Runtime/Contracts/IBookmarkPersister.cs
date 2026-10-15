using Grove.Workflows.Runtime.Requests;

namespace Grove.Workflows.Runtime;

/// <summary>
/// Persists bookmarks and raises events.
/// </summary>
public interface IBookmarksPersister
{
    /// <summary>
    /// Persists bookmarks and raises events.
    /// </summary>
    Task PersistBookmarksAsync(UpdateBookmarksRequest updateBookmarksRequest);
}