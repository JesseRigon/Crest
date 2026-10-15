using Grove.Workflows.Expressions.Contracts;
using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Extensions;
using Grove.Workflows.Models;

namespace Grove.Workflows.Expressions;

/// <summary>
/// Evaluates an <see cref="Output"/> expression.
/// </summary>
public class OutputExpressionHandler : IExpressionHandler
{
    /// <inheritdoc />
    public ValueTask<object?> EvaluateAsync(Expression expression, Type returnType, ExpressionExecutionContext context, ExpressionEvaluatorOptions options)
    {
        var value = expression.Value is Output output ? context.Get(output) : default;
        return ValueTask.FromResult(value);
    }
}