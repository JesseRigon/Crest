namespace Crest.Rules.Services;

public interface IConditionResolver
{
    IConditionEvaluator GetConditionEvaluator(Condition condition);
}
