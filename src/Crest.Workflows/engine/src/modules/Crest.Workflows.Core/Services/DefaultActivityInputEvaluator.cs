using Crest.Workflows.Extensions;

namespace Crest.Workflows;

public class DefaultActivityInputEvaluator : IActivityInputEvaluator
{
    public async Task<object?> EvaluateAsync(ActivityInputEvaluatorContext context)
    {
        var wrappedInput = context.Input;
        var evaluator = context.ExpressionEvaluator;
        var expressionExecutionContext = context.ExpressionExecutionContext;
        return await evaluator.EvaluateAsync(wrappedInput, expressionExecutionContext);
    }
}