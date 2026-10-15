namespace Grove.Display.Rules.Models;

public class UrlCondition : Condition
{
    public string Value { get; set; }
    public ConditionOperator Operation { get; set; }
}
