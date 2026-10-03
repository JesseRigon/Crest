using Crest.Workflows.Common.Entities;
using Crest.Workflows.Runtime.Entities;
using Crest.Workflows.Runtime.Filters;
using Crest.Workflows.Runtime.OrderDefinitions;
using Crest.Workflows.Documents;
using Crest.Workflows.Indexes;
using YesSql;

namespace Crest.Workflows.Extensions;

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