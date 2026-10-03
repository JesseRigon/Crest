using Crest.Workflows.Runtime.Requests;

namespace Crest.Workflows.Runtime;

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