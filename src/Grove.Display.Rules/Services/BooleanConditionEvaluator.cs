using Grove.Display.Rules.Models;

namespace Grove.Display.Rules.Services;

public class BooleanConditionEvaluator : ConditionEvaluator<BooleanCondition>
{
    public override ValueTask<bool> EvaluateAsync(BooleanCondition condition)
        => condition.Value ? True : False;
}
