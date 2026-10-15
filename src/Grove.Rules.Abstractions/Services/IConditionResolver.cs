namespace Grove.Rules.Services;

public interface IConditionResolver
{
    IConditionEvaluator GetConditionEvaluator(Condition condition);
}
