using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Documents;
using Crest.Workflows.Indexes;
using YesSql;

namespace Crest.Workflows.Extensions;

public static class StoredBookmarkQueryExtensions
{
    public static IQuery<StoredBookmarkDocument, StoredBookmarkIndex> Apply(this IQuery<StoredBookmarkDocument, StoredBookmarkIndex> query, BookmarkFilter filter)
    {
        return filter.Apply(query);
    }
}