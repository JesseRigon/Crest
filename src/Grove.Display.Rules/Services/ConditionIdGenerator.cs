using Grove.Entities;

namespace Grove.Display.Rules.Services;

public class ConditionIdGenerator : IConditionIdGenerator
{
    private readonly IIdGenerator _idGenerator;

    public ConditionIdGenerator(IIdGenerator idGenerator)
    {
        _idGenerator = idGenerator;
    }

    public void GenerateUniqueId(Condition condition)
    {
        condition.ConditionId = _idGenerator.GenerateUniqueId();
    }
}
