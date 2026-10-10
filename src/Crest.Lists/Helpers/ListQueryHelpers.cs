using System.Linq.Expressions;
using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using Crest.Lists.Indexes;
using YesSql;

namespace Crest.Lists.Helpers;

internal static class ListQueryHelpers
{
    internal static Task<int> QueryListItemsCountAsync(ISession session, string listContentItemId, Expression<Func<ContentItemIndex, bool>> itemPredicate = null)
    {
        return session.Query<ContentItem>()
                .With<ContainedPartIndex>(x => x.ListContentItemId == listContentItemId)
                .With<ContentItemIndex>(itemPredicate ?? (x => x.Published))
                .CountAsync();
    }

    internal static async Task<IEnumerable<ContentItem>> QueryListItemsAsync(ISession session, string listContentItemId, Expression<Func<ContentItemIndex, bool>> itemPredicate = null)
    {
        return await session.Query<ContentItem>()
                .With<ContainedPartIndex>(x => x.ListContentItemId == listContentItemId)
                .OrderBy(o => o.Order)
                .ThenBy(o => o.Id)
                .With<ContentItemIndex>(itemPredicate ?? (x => x.Published))
                .ListAsync();
    }
}
