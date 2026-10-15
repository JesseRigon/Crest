using System.Diagnostics;
using Grove.Workflows.Expressions.Contracts;
using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Extensions;
using Grove.Workflows.Memory;
using Grove.Workflows.Models;

namespace Grove.Workflows.Expressions;

/// <summary>
/// Handles Input expressions.
/// </summary>
public class InputExpressionHandler : IExpressionHandler
{
    /// <inheritdoc />
    public ValueTask<object?> EvaluateAsync(Expression expression, Type returnType, ExpressionExecutionContext context, ExpressionEvaluatorOptions options)
    {
        object? result = null;
        var inputDefinition = expression.Value as InputDefinition;

        if (inputDefinition != null)
        {
            result = context.GetInput(inputDefinition.Name);
        }

        return ValueTask.FromResult(result);
    }
}