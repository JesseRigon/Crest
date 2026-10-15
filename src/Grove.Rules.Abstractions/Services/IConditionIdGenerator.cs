namespace Grove.Rules.Services;

public interface IConditionIdGenerator
{
    void GenerateUniqueId(Condition condition);
}
