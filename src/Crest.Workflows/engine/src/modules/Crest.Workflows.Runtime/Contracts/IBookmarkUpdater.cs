using Crest.Workflows.Runtime.Requests;

namespace Crest.Workflows.Runtime;

public interface IBookmarkUpdater
{
    Task UpdateBookmarksAsync(UpdateBookmarksRequest request, CancellationToken cancellationToken = default);
}