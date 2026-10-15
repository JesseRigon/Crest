using Grove.Workflows.Runtime;
using Grove.Workflows.Runtime.Filters;

// ReSharper disable once CheckNamespace
namespace Grove.Workflows.Extensions;

public static class BookmarkQueueItemExtensions
{
    public static Task DeleteAsync(this IBookmarkQueueStore store, string id, CancellationToken cancellationToken = default)
    {
        var filter = new BookmarkQueueFilter
        {
            Id = id
        };

        return store.DeleteAsync(filter, cancellationToken);
    }
}