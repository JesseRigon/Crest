namespace Grove.Display.Rules;

public interface IConditionEvaluator
{
    ValueTask<bool> EvaluateAsync(Condition condition);
}
