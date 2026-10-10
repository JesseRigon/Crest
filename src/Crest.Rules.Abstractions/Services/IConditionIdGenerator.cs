namespace Crest.Rules.Services;

public interface IConditionIdGenerator
{
    void GenerateUniqueId(Condition condition);
}
