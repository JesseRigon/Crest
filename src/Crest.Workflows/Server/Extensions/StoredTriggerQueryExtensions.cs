using Crest.Workflows.Common.Entities;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.OrderDefinitions;
using Crest.Workflows.Documents;
using Crest.Workflows.Indexes;
using YesSql;

namespace Crest.Workflows.Extensions;

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