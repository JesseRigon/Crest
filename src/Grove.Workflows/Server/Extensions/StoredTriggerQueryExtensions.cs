using Grove.Workflows.Common.Entities;
using Grove.Workflows.Runtime.Entities;
using Grove.Workflows.Runtime.Filters;
using Grove.Workflows.Runtime.OrderDefinitions;
using Grove.Workflows.Documents;
using Grove.Workflows.Indexes;
using YesSql;

namespace Grove.Workflows.Extensions;

public static class StoredTriggerQueryExtensions
{
    public static IQuery<StoredTriggerDocument, StoredTriggerIndex> Apply(this IQuery<StoredTriggerDocument, StoredTriggerIndex> query, TriggerFilter filter)
    {
        return filter.Apply(query);
    }

    public static IQueryIndex<StoredTriggerIndex> Apply(this IQueryIndex<StoredTriggerIndex> query, TriggerFilter filter)
    {
        return filter.Apply(query);
    }

    public static IQuery<StoredTriggerDocument, StoredTriggerIndex> Apply<TOrderBy>(this IQuery<StoredTriggerDocument, StoredTriggerIndex> query, StoredTriggerOrder<TOrderBy> order)
    {
        var keySelector = ExpressionConverter.Convert<StoredTrigger, StoredTriggerIndex, TOrderBy>(order.KeySelector);
        return order.Direction == OrderDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }

    public static IQueryIndex<StoredTriggerIndex> Apply<TOrderBy>(this IQueryIndex<StoredTriggerIndex> query, StoredTriggerOrder<TOrderBy> order)
    {
        var keySelector = ExpressionConverter.Convert<StoredTrigger, StoredTriggerIndex, TOrderBy>(order.KeySelector);
        return order.Direction == OrderDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}