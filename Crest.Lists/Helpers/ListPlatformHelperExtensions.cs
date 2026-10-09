using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Crest;
using Crest.ContentManagement;
using Crest.ContentManagement.Records;
using Crest.Lists.Helpers;
using YesSql;

#pragma warning disable CA1050 // Declare types in namespaces
public static class ListPlatformHelperExtensions
#pragma warning restore CA1050 // Declare types in namespaces
{
    /// <summary>
    /// Returns list count.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="listContentItemId">The list content item id.</param>
    /// <param name="itemPredicate">The optional predicate applied to each item. By defult published items only.</param>
    /// <returns>A number of list items satisfying given predicate.</returns>
    public static Task<int> QueryListItemsCountAsync(this IPlatformHelper platformHelper, string listContentItemId, Expression<Func<ContentItemIndex, bool>> itemPredicate = null)
    {
        var session = platformHelper.HttpContext.RequestServices.GetService<ISession>();

        return ListQueryHelpers.QueryListItemsCountAsync(session, listContentItemId, itemPredicate);
    }

    /// <summary>
    /// Returns list items.
    /// </summary>
    /// <param name="platformHelper">The <see cref="IPlatformHelper"/>.</param>
    /// <param name="listContentItemId">The list content item id.</param>
    /// <param name="itemPredicate">The optional predicate applied to each item. By defult published items only.</param>
    /// <returns>An enumerable of list items satisfying given predicate.</returns>
    public static Task<IEnumerable<ContentItem>> QueryListItemsAsync(this IPlatformHelper platformHelper, string listContentItemId, Expression<Func<ContentItemIndex, bool>> itemPredicate = null)
    {
        var session = platformHelper.HttpContext.RequestServices.GetService<ISession>();

        return ListQueryHelpers.QueryListItemsAsync(session, listContentItemId, itemPredicate);
    }
}
