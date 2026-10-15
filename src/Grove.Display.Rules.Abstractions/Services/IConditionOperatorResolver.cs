namespace Grove.Display.Rules.Services;

public interface IConditionOperatorResolver
{
    IOperatorComparer GetOperatorComparer(ConditionOperator conditionOperator);
}
