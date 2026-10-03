using Crest.Workflows.Expressions.Contracts;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Extensions;
using Crest.Workflows.Models;

namespace Crest.Workflows.Expressions;

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