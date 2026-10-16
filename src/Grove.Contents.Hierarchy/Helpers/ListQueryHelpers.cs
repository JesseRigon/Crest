using System.Linq.Expressions;
using Grove.Contents.Management;
using Grove.Contents.Management.Records;
using Grove.Contents.Hierarchy.Indexes;
using YesSql;

namespace Grove.Contents.Hierarchy.Helpers;

internal static class ListQueryHelpers
{
    internal static Task<int> QueryListItemsCountAsync(ISession session, string listContentItemId, Expression<Func<ContentItemIndex, bool>> itemPredicate = null)
    {
        return session.Query<ContentItem>()
                .With<ParentContentItemIndex>(x => x.ListContentItemId == listContentItemId)
                .With<ContentItemIndex>(itemPredicate ?? (x => x.Published))
                .CountAsync();
    }

    internal static async Task<IEnumerable<ContentItem>> QueryListItemsAsync(ISession session, string listContentItemId, Expression<Func<ContentItemIndex, bool>> itemPredicate = null)
    {
        return await session.Query<ContentItem>()
                .With<ParentContentItemIndex>(x => x.ListContentItemId == listContentItemId)
                .OrderBy(o => o.Order)
                .ThenBy(o => o.Id)
                .With<ContentItemIndex>(itemPredicate ?? (x => x.Published))
                .ListAsync();
    }
}
