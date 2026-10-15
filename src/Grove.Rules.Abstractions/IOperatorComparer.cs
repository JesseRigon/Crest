namespace Grove.Rules;

public interface IOperatorComparer
{
    bool Compare(ConditionOperator ruleOperation, object a, object b);
}
