using System.Linq.Expressions;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Management.Entities;
using JetBrains.Annotations;

namespace Crest.Workflows.Management.Filters;

/// <summary>
/// Represents the order by which to order the results of a query.
/// </summary>
[PublicAPI]
public class WorkflowDefinitionOrder<TProp> : OrderDefinition<WorkflowDefinition, TProp>
{
    /// <inheritdoc />
    public WorkflowDefinitionOrder()
    {
    }

    /// <summary>
    /// Creates a new instance of the <see cref="WorkflowDefinitionOrder{TProp}"/> class.
    /// </summary>
    public WorkflowDefinitionOrder(Expression<Func<WorkflowDefinition, TProp>> keySelector, OrderDirection direction) : base(keySelector, direction)
    {
    }
}