using Crest.Workflows.Runtime;
using Crest.Workflows.Runtime.Filters;

// ReSharper disable once CheckNamespace
namespace Crest.Workflows.Extensions;

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