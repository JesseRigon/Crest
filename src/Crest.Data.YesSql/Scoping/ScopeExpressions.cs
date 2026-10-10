using System.Linq.Expressions;
using System.Reflection;
using YesSql.Services;

using Crest.Access;

namespace Crest.Data.Scoping;

/// <summary>
/// Compiles a <see cref="ScopeRule"/> into a YesSql index predicate, so content lists and
/// pickers apply the same scope the query pipeline conjoins into SQL.
/// </summary>
public static class ScopeExpressions
{
    // YesSql: bool IsIn(this object source, IEnumerable values) / IsNotIn(...), translated to
    // "IN (...)" and "NOT IN (...)".
    private static readonly MethodInfo IsInMethod = typeof(DefaultQueryExtensions).GetMethod(nameof(DefaultQueryExtensions.IsIn), [typeof(object), typeof(System.Collections.IEnumerable)])!;

    private static readonly MethodInfo IsNotInMethod = typeof(DefaultQueryExtensions).GetMethod(nameof(DefaultQueryExtensions.IsNotIn), [typeof(object), typeof(System.Collections.IEnumerable)])!;

    public static Expression<Func<TIndex, bool>> ToPredicate<TIndex>(ScopeRule rule)
    {
        var parameter = Expression.Parameter(typeof(TIndex), "index");
        var body = rule.Kind switch
        {
            ScopeKind.All => Expression.Constant(true),
            ScopeKind.None => Expression.Constant(false),
            _ => Translate(rule.Filter!, parameter),
        };

        return Expression.Lambda<Func<TIndex, bool>>(body, parameter);
    }

    private static Expression Translate(ScopeFilter filter, ParameterExpression parameter)
    {
        if (filter.Condition is { } condition)
        {
            var property = parameter.Type.GetProperty(condition.Column, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"Index '{parameter.Type.Name}' has no column '{condition.Column}'.");
            var member = Expression.Property(parameter, property);

            if (condition.Operator is ScopeOperator.In or ScopeOperator.NotIn)
            {
                var negate = condition.Operator == ScopeOperator.NotIn;
                if (condition.Values.Count == 0)
                {
                    return Expression.Constant(negate);
                }

                var elementType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var typedValues = Array.CreateInstance(property.PropertyType, condition.Values.Count);
                for (var i = 0; i < condition.Values.Count; i++)
                {
                    typedValues.SetValue(condition.Values[i] is null ? null : Convert.ChangeType(condition.Values[i], elementType, System.Globalization.CultureInfo.InvariantCulture), i);
                }

                var values = Expression.Constant(typedValues, typeof(System.Collections.IEnumerable));
                return Expression.Call(negate ? IsNotInMethod : IsInMethod, Expression.Convert(member, typeof(object)), values);
            }

            var single = condition.Values.Count > 0 ? condition.Values[0] : null;
            var typed = single is null ? null : Convert.ChangeType(single, Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType, System.Globalization.CultureInfo.InvariantCulture);
            return Expression.Equal(member, Expression.Constant(typed, property.PropertyType));
        }

        if (filter.All is { Count: > 0 } all)
        {
            return all.Select(part => Translate(part, parameter)).Aggregate(Expression.AndAlso);
        }

        if (filter.Any is { Count: > 0 } any)
        {
            return any.Select(part => Translate(part, parameter)).Aggregate(Expression.OrElse);
        }

        return Expression.Constant(false);
    }
}
