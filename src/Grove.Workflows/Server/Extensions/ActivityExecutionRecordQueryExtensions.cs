using Grove.Workflows.Common.Entities;
using Grove.Workflows.Runtime.Entities;
using Grove.Workflows.Runtime.Filters;
using Grove.Workflows.Runtime.OrderDefinitions;
using Grove.Workflows.Documents;
using Grove.Workflows.Indexes;
using YesSql;

namespace Grove.Workflows.Extensions;

public static class ActivityExecutionRecordQueryExtensions
{
    public static IQuery<ActivityExecutionRecordDocument, ActivityExecutionRecordIndex> Apply(this IQuery<ActivityExecutionRecordDocument, ActivityExecutionRecordIndex> query, ActivityExecutionRecordFilter filter)
    {
        return filter.Apply(query);
    }
    
    public static IQuery<ActivityExecutionRecordDocument, ActivityExecutionRecordIndex> Apply<TOrderBy>(this IQuery<ActivityExecutionRecordDocument, ActivityExecutionRecordIndex> query, ActivityExecutionRecordOrder<TOrderBy> order)
    {
        var keySelector = ExpressionConverter.Convert<ActivityExecutionRecord, ActivityExecutionRecordIndex, TOrderBy>(order.KeySelector);
        return order.Direction == OrderDirection.Ascending 
            ? query.OrderBy(keySelector) 
            : query.OrderByDescending(keySelector);
    }
    
    public static IQueryIndex<ActivityExecutionRecordIndex> Apply(this IQueryIndex<ActivityExecutionRecordIndex> query, ActivityExecutionRecordFilter filter)
    {
        return filter.Apply(query);
    }
    
    public static IQueryIndex<ActivityExecutionRecordIndex> Apply<TOrderBy>(this IQueryIndex<ActivityExecutionRecordIndex> query, ActivityExecutionRecordOrder<TOrderBy> order)
    {
        var keySelector = ExpressionConverter.Convert<ActivityExecutionRecord, ActivityExecutionRecordIndex, TOrderBy>(order.KeySelector);
        return order.Direction == OrderDirection.Ascending 
            ? query.OrderBy(keySelector) 
            : query.OrderByDescending(keySelector);
    }
}