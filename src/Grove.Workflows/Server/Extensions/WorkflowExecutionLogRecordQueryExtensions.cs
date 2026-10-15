using Grove.Workflows.Common.Entities;
using Grove.Workflows.Runtime.Entities;
using Grove.Workflows.Runtime.Filters;
using Grove.Workflows.Runtime.OrderDefinitions;
using Grove.Workflows.Documents;
using Grove.Workflows.Indexes;
using YesSql;

namespace Grove.Workflows.Extensions;

public static class WorkflowExecutionLogRecordQueryExtensions
{
    public static IQuery<WorkflowExecutionLogRecordDocument, WorkflowExecutionLogRecordIndex> Apply(this IQuery<WorkflowExecutionLogRecordDocument, WorkflowExecutionLogRecordIndex> query, WorkflowExecutionLogRecordFilter filter)
    {
        return filter.Apply(query);
    }

    public static IQuery<WorkflowExecutionLogRecordDocument, WorkflowExecutionLogRecordIndex> Apply<TOrderBy>(this IQuery<WorkflowExecutionLogRecordDocument, WorkflowExecutionLogRecordIndex> query, WorkflowExecutionLogRecordOrder<TOrderBy> order)
    {
        var keySelector = ExpressionConverter.Convert<WorkflowExecutionLogRecord, WorkflowExecutionLogRecordIndex, TOrderBy>(order.KeySelector);
        return order.Direction == OrderDirection.Ascending
            ? query.OrderBy(keySelector)
            : query.OrderByDescending(keySelector);
    }
}