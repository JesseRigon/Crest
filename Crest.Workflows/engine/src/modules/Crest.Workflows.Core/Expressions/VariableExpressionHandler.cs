using Crest.Workflows.Expressions.Contracts;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Memory;

namespace Crest.Workflows.Expressions;

/// <summary>
/// Handles Variable expressions.
/// </summary>
public class VariableExpressionHandler : IExpressionHandler
{
    /// <inheritdoc />
    public ValueTask<object?> EvaluateAsync(Expression expression, Type returnType, ExpressionExecutionContext context, ExpressionEvaluatorOptions options)
    {
        var variable = expression.Value as Variable;
        var value = variable?.Get(context);
        return ValueTask.FromResult(value);
    }
}