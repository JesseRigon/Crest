using Grove.Workflows.Common.Entities;
using Grove.Workflows.Management.Entities;
using Grove.Workflows.Management.Filters;
using Grove.Workflows.Documents;
using Grove.Workflows.Indexes;
using YesSql;

namespace Grove.Workflows.Extensions;

public static class WorkflowInstanceQueryExtensions
{
    public static IQuery<WorkflowInstanceDocument, WorkflowInstanceIndex> Apply(this IQuery<WorkflowInstanceDocument, WorkflowInstanceIndex> query, WorkflowInstanceFilter filter)
    {
        return filter.Apply(query);
    }

    public static IQuery<WorkflowInstanceDocument, WorkflowInstanceIndex> Apply<TOrderBy>(this IQuery<WorkflowInstanceDocument, WorkflowInstanceIndex> query, WorkflowInstanceOrder<TOrderBy> order)
    {
        var keySelector = ExpressionConverter.Convert<WorkflowInstance, WorkflowInstanceIndex, TOrderBy>(order.KeySelector);
        return order.Direction == OrderDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }

    public static IQueryIndex<WorkflowInstanceIndex> Apply(this IQueryIndex<WorkflowInstanceIndex> query, WorkflowInstanceFilter filter)
    {
        return filter.Apply(query);
    }

    public static IQueryIndex<WorkflowInstanceIndex> Apply<TOrderBy>(this IQueryIndex<WorkflowInstanceIndex> query, WorkflowInstanceOrder<TOrderBy> order)
    {
        var keySelector = ExpressionConverter.Convert<WorkflowInstance, WorkflowInstanceIndex, TOrderBy>(order.KeySelector);
        return order.Direction == OrderDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}