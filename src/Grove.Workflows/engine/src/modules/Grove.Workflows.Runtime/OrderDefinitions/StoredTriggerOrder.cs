using System.Linq.Expressions;
using Grove.Workflows.Common.Entities;
using Grove.Workflows.Runtime.Entities;

namespace Grove.Workflows.Runtime.OrderDefinitions;

public class StoredTriggerOrder<TProp> : OrderDefinition<StoredTrigger, TProp>
{
    public StoredTriggerOrder(Expression<Func<StoredTrigger, TProp>> keySelector, OrderDirection direction)
    {
        KeySelector = keySelector;
        Direction = direction;
    }
}