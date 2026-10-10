using Crest.Rules.Models;

namespace Crest.Rules.Services;

public class BooleanConditionEvaluator : ConditionEvaluator<BooleanCondition>
{
    public override ValueTask<bool> EvaluateAsync(BooleanCondition condition)
        => condition.Value ? True : False;
}
