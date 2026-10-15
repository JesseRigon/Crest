using System.Dynamic;
using Grove.Workflows.Expressions.Contracts;
using Grove.Workflows.Expressions.Helpers;
using Grove.Workflows.Expressions.Models;
using Grove.Workflows.Expressions.JavaScript.Contracts;
using Humanizer;
using Jint;

namespace Grove.Workflows.Expressions.JavaScript.Expressions;

/// <summary>
/// Evaluates JavaScript expressions.
/// </summary>
public class JavaScriptExpressionHandler : IExpressionHandler
{
    private readonly IJavaScriptEvaluator _javaScriptEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="JavaScriptExpressionHandler"/> class.
    /// </summary>
    public JavaScriptExpressionHandler(IJavaScriptEvaluator javaScriptEvaluator)
    {
        _javaScriptEvaluator = javaScriptEvaluator;
    }

    /// <inheritdoc />
    public async ValueTask<object?> EvaluateAsync(Expression expression, Type returnType, ExpressionExecutionContext context, ExpressionEvaluatorOptions options)
    {
        var javaScriptExpression = expression.Value.ConvertTo<string>() ?? "";
        return await _javaScriptEvaluator.EvaluateAsync(javaScriptExpression, returnType, context, options, engine => ConfigureEngine(engine, options), context.CancellationToken);
    }

    private void ConfigureEngine(Engine engine, ExpressionEvaluatorOptions options)
    {
        var args = new ExpandoObject() as IDictionary<string, object>;

        foreach (var (name, value) in options.Arguments)
            args[name.Camelize()] = value;

        engine.SetValue("args", args);
    }
}