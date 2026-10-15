using Grove.Access;
using Grove.Display.Rules.Models;

namespace Grove.Display.Rules.Services;

/// <summary>Evaluates a role condition against the request's caller, never the principal.</summary>
public class RoleConditionEvaluator(ICallerContextAccessor callers, IConditionOperatorResolver operatorResolver) : ConditionEvaluator<RoleCondition>
{
    public override ValueTask<bool> EvaluateAsync(RoleCondition condition)
    {
        IEnumerable<string> roles = callers.Current?.Roles ?? Enumerable.Empty<string>();
        var operatorComparer = operatorResolver.GetOperatorComparer(condition.Operation);

        // Claim all if the operator is negative
        if (condition.Operation is INegateOperator)
        {
            return roles.All(role => operatorComparer.Compare(condition.Operation, role, condition.Value)) ? True : False;
        }

        return roles.Any(role => operatorComparer.Compare(condition.Operation, role, condition.Value)) ? True : False;
    }
}
