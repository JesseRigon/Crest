namespace Grove.Display.Rules.Services;

public interface IRuleService
{
    ValueTask<bool> EvaluateAsync(Rule rule);
}
