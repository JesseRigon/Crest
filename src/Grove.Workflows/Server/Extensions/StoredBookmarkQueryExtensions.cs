using Grove.Workflows.Runtime.Entities;
using Grove.Workflows.Runtime.Filters;
using Grove.Workflows.Documents;
using Grove.Workflows.Indexes;
using YesSql;

namespace Grove.Workflows.Extensions;

public static class StoredBookmarkQueryExtensions
{
    public static IQuery<StoredBookmarkDocument, StoredBookmarkIndex> Apply(this IQuery<StoredBookmarkDocument, StoredBookmarkIndex> query, BookmarkFilter filter)
    {
        return filter.Apply(query);
    }
}