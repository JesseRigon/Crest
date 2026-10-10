using System.Diagnostics;
using Crest.Workflows.Expressions.Contracts;
using Crest.Workflows.Expressions.Models;
using Crest.Workflows.Extensions;
using Crest.Workflows.Memory;
using Crest.Workflows.Models;

namespace Crest.Workflows.Expressions;

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