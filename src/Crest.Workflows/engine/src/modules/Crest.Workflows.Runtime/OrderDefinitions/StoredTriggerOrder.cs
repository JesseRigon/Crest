using System.Linq.Expressions;
using Crest.Workflows.Common.Entities;
using Crest.Workflows.Runtime.Entities;

namespace Crest.Workflows.Runtime.OrderDefinitions;

public class StoredTriggerOrder<TProp> : OrderDefinition<StoredTrigger, TProp>
{
    public StoredTriggerOrder(Expression<Func<StoredTrigger, TProp>> keySelector, OrderDirection direction)
    {
        KeySelector = keySelector;
        Direction = direction;
    }
}