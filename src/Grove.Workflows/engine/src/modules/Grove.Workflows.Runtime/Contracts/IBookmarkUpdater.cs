using Grove.Workflows.Runtime.Requests;

namespace Grove.Workflows.Runtime;

public interface IBookmarkUpdater
{
    Task UpdateBookmarksAsync(UpdateBookmarksRequest request, CancellationToken cancellationToken = default);
}