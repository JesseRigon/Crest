using Grove.Display.Management.Components;
using Fluid;
using Fluid.Ast;
using Fluid.Ast.BinaryExpressions;
using Fluid.Values;

namespace Grove.Display.Templates.Services;

/// <summary>
/// Comparisons in bindings (docs/widgets.md, ruling 2026-10-13): a tag argument such as
/// <c>visible: nodes.country.value == "US"</c> is read off the parsed expression, never
/// evaluated: the left side a binding source, the operator one of the fixed list, the right side
/// a literal or another source; <c>and</c> / <c>or</c> join, <c>not</c> negates (and is how
/// <c>&gt;=</c> and <c>&lt;=</c> are written down). Liquid's <c>startswith</c>, <c>endswith</c>
/// and <c>contains</c> are the text operators, <c>== empty</c> and <c>!= empty</c> the emptiness
/// ones. A comparison with no binding in it is not a predicate: Fluid evaluates it as usual.
/// </summary>
public static class TemplateTreePredicates
{
    /// <summary>The argument's value: a predicate as a binding, else whatever the expression evaluates to.</summary>
    public static async ValueTask<FluidValue> EvaluateArgumentAsync(Expression expression, TemplateContext context) =>
        await TryBuildAsync(expression, context) is { } predicate ? new BindingValue(predicate) : await expression.EvaluateAsync(context);

    /// <summary><c>not</c> before a comparison: its negation. Evaluated plainly when no binding is in it.</summary>
    public sealed class NotExpression(Expression operand) : Expression
    {
        public Expression Operand { get; } = operand;

        public override async ValueTask<FluidValue> EvaluateAsync(TemplateContext context) =>
            BooleanValue.Create(!(await Operand.EvaluateAsync(context)).ToBooleanValue());
    }

    public static ValueTask<DisplayBinding?> TryBuildAsync(Expression expression, TemplateContext context) => expression switch
    {
        NotExpression not => NegateAsync(not.Operand, context),
        AndBinaryExpression and => CombineAsync(DisplayPredicates.And, and.Left, and.Right, context),
        OrBinaryExpression or => CombineAsync(DisplayPredicates.Or, or.Left, or.Right, context),
        EqualBinaryExpression equal => CompareAsync(DisplayPredicates.Equal, equal.Left, equal.Right, context),
        NotEqualBinaryExpression notEqual => CompareAsync(DisplayPredicates.NotEqual, notEqual.Left, notEqual.Right, context),
        ContainsBinaryExpression contains => CompareAsync(DisplayPredicates.Contains, contains.Left, contains.Right, context),
        StartsWithBinaryExpression startsWith => CompareAsync(DisplayPredicates.StartsWith, startsWith.Left, startsWith.Right, context),
        EndsWithBinaryExpression endsWith => CompareAsync(DisplayPredicates.EndsWith, endsWith.Left, endsWith.Right, context),
        GreaterThanBinaryExpression greater => OrderedAsync(DisplayPredicates.Greater, greater.Strict, greater.Left, greater.Right, context),
        LowerThanBinaryExpression lower => OrderedAsync(DisplayPredicates.Less, lower.Strict, lower.Left, lower.Right, context),
        _ => new((DisplayBinding?)null),
    };

    private static async ValueTask<DisplayBinding?> NegateAsync(Expression operand, TemplateContext context)
    {
        var (binding, _) = await TermAsync(operand, context);
        return binding is null ? null : DisplayBinding.Combine(DisplayPredicates.Not, [binding]);
    }

    // ">=" is "not <", "<=" is "not >": the grammar has only the strict operators.
    private static async ValueTask<DisplayBinding?> OrderedAsync(string @operator, bool strict, Expression left, Expression right, TemplateContext context)
    {
        if (strict)
        {
            return await CompareAsync(@operator, left, right, context);
        }

        var opposite = @operator == DisplayPredicates.Greater ? DisplayPredicates.Less : DisplayPredicates.Greater;
        return await CompareAsync(opposite, left, right, context) is { } comparison ? DisplayBinding.Combine(DisplayPredicates.Not, [comparison]) : null;
    }

    private static async ValueTask<DisplayBinding?> CompareAsync(string @operator, Expression left, Expression right, TemplateContext context)
    {
        var (leftBinding, leftValue) = await TermAsync(left, context);
        var (rightBinding, rightValue) = await TermAsync(right, context);

        if (leftBinding is null && rightBinding is not null && @operator is DisplayPredicates.Equal or DisplayPredicates.NotEqual or DisplayPredicates.Greater or DisplayPredicates.Less)
        {
            // "US" == nodes.country.value: the source goes left; an ordering flips with it.
            (leftBinding, leftValue, rightBinding, rightValue) = (rightBinding, rightValue, leftBinding, leftValue);
            @operator = @operator switch { DisplayPredicates.Greater => DisplayPredicates.Less, DisplayPredicates.Less => DisplayPredicates.Greater, _ => @operator };
        }

        if (leftBinding is null || leftBinding.IsPredicate)
        {
            return null;
        }

        if (rightBinding is null && rightValue is EmptyValue && @operator is DisplayPredicates.Equal or DisplayPredicates.NotEqual)
        {
            return DisplayBinding.Compare(leftBinding, @operator == DisplayPredicates.Equal ? DisplayPredicates.Empty : DisplayPredicates.NotEmpty);
        }

        return DisplayBinding.Compare(leftBinding, @operator, rightBinding ?? DisplayBinding.Literal(TemplateTreeState.ToJson(rightValue!)));
    }

    private static async ValueTask<DisplayBinding?> CombineAsync(string combinator, Expression left, Expression right, TemplateContext context)
    {
        var (leftBinding, leftValue) = await TermAsync(left, context);
        var (rightBinding, rightValue) = await TermAsync(right, context);
        if (leftBinding is null && rightBinding is null)
        {
            return null;
        }

        return DisplayBinding.Combine(combinator,
        [
            leftBinding ?? DisplayBinding.Literal(TemplateTreeState.ToJson(leftValue!)),
            rightBinding ?? DisplayBinding.Literal(TemplateTreeState.ToJson(rightValue!)),
        ]);
    }

    // A side of a comparison: a nested predicate, a binding source, or a plain value.
    private static async ValueTask<(DisplayBinding? Binding, FluidValue? Value)> TermAsync(Expression expression, TemplateContext context)
    {
        if (await TryBuildAsync(expression, context) is { } predicate)
        {
            return (predicate, null);
        }

        var value = await expression.EvaluateAsync(context);
        return value is BindingValue binding ? (binding.Binding, null) : (null, value);
    }
}
