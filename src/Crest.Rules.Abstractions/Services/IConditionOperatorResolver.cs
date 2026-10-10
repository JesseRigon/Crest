namespace Crest.Rules.Services;

public interface IConditionOperatorResolver
{
    IOperatorComparer GetOperatorComparer(ConditionOperator conditionOperator);
}
