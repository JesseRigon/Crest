namespace Grove.Display.Rules.Services;

public interface IConditionResolver
{
    IConditionEvaluator GetConditionEvaluator(Condition condition);
}
