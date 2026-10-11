using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using YesSql.Indexes;
using YesSql.Services;

using Crest.Access;

namespace Crest.Data.Scoping;

/// <summary>
/// Compiles a <see cref="ScopeRule"/> into a YesSql index predicate, so content lists and
/// pickers apply the same scope the query pipeline conjoins into SQL. A semi-join
/// (<see cref="ScopeOperator.InSelect"/>) compiles to YesSql's <c>IsIn&lt;TIndex&gt;</c>
/// subquery over the index type named by its table: found among the loaded index types by
/// name (the table name without the tenant prefix), or by the resolver a caller passes.
/// </summary>
public static class ScopeExpressions
{
    // YesSql: bool IsIn(this object source, IEnumerable values) / IsNotIn(...), translated to
    // "IN (...)" and "NOT IN (...)".
    private static readonly MethodInfo IsInMethod = typeof(DefaultQueryExtensions).GetMethod(nameof(DefaultQueryExtensions.IsIn), [typeof(object), typeof(System.Collections.IEnumerable)])!;

    private static readonly MethodInfo IsNotInMethod = typeof(DefaultQueryExtensions).GetMethod(nameof(DefaultQueryExtensions.IsNotIn), [typeof(object), typeof(System.Collections.IEnumerable)])!;

    // YesSql: bool IsIn<TIndex>(this object source, Expression<Func<TIndex, object>> select,
    // Expression<Func<TIndex, bool>> where) / IsNotIn<TIndex>, translated to
    // "IN (SELECT select FROM TIndex WHERE where)".
    private static readonly MethodInfo IsInSelectMethod = typeof(DefaultQueryExtensionsIndex).GetMethods().Single(method => method.Name == nameof(DefaultQueryExtensionsIndex.IsIn) && method.IsGenericMethodDefinition && method.GetParameters().Length == 3);

    private static readonly MethodInfo IsNotInSelectMethod = typeof(DefaultQueryExtensionsIndex).GetMethods().Single(method => method.Name == nameof(DefaultQueryExtensionsIndex.IsNotIn) && method.IsGenericMethodDefinition && method.GetParameters().Length == 3);

    private static readonly ConcurrentDictionary<string, Type> IndexTypes = new(StringComparer.OrdinalIgnoreCase);

    public static Expression<Func<TIndex, bool>> ToPredicate<TIndex>(ScopeRule rule) => ToPredicate<TIndex>(rule, indexTypes: null);

    /// <param name="indexTypes">The index type of a semi-join's table; null (or a null answer)
    /// looks it up among the loaded YesSql index types by name.</param>
    public static Expression<Func<TIndex, bool>> ToPredicate<TIndex>(ScopeRule rule, Func<string, Type?>? indexTypes)
    {
        var parameter = Expression.Parameter(typeof(TIndex), "index");
        var body = rule.Kind switch
        {
            ScopeKind.All => Expression.Constant(true),
            ScopeKind.None => Expression.Constant(false),
            _ => Translate(rule.Filter!, parameter, indexTypes),
        };

        return Expression.Lambda<Func<TIndex, bool>>(body, parameter);
    }

    private static Expression Translate(ScopeFilter filter, ParameterExpression parameter, Func<string, Type?>? indexTypes)
    {
        if (filter.Condition is { } condition)
        {
            var property = parameter.Type.GetProperty(condition.Column, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"Index '{parameter.Type.Name}' has no column '{condition.Column}'.");
            var member = Expression.Property(parameter, property);
            var elementType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            switch (condition.Operator)
            {
                case ScopeOperator.In or ScopeOperator.NotIn:
                    var negate = condition.Operator == ScopeOperator.NotIn;
                    if (condition.Values.Count == 0)
                    {
                        return Expression.Constant(negate);
                    }

                    var typedValues = Array.CreateInstance(property.PropertyType, condition.Values.Count);
                    for (var i = 0; i < condition.Values.Count; i++)
                    {
                        typedValues.SetValue(condition.Values[i] is null ? null : Convert(condition.Values[i]!, elementType), i);
                    }

                    var values = Expression.Constant(typedValues, typeof(System.Collections.IEnumerable));
                    return Expression.Call(negate ? IsNotInMethod : IsInMethod, Expression.Convert(member, typeof(object)), values);

                case ScopeOperator.IsNull:
                    return property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null
                        ? Expression.Constant(false)
                        : Expression.Equal(member, Expression.Constant(null, property.PropertyType));

                case ScopeOperator.LessThan or ScopeOperator.LessThanOrEqual or ScopeOperator.GreaterThan or ScopeOperator.GreaterThanOrEqual:
                    var operand = condition.Values.Count > 0 ? condition.Values[0] : null;
                    if (operand is null)
                    {
                        // A comparison with null admits nothing, as in SQL.
                        return Expression.Constant(false);
                    }

                    if (elementType == typeof(string))
                    {
                        throw new NotSupportedException($"A scope comparison on the text column '{condition.Column}' cannot be compiled into an index predicate.");
                    }

                    var constant = Expression.Constant(Convert(operand, elementType), property.PropertyType);
                    return condition.Operator switch
                    {
                        ScopeOperator.LessThan => Expression.LessThan(member, constant),
                        ScopeOperator.LessThanOrEqual => Expression.LessThanOrEqual(member, constant),
                        ScopeOperator.GreaterThan => Expression.GreaterThan(member, constant),
                        _ => Expression.GreaterThanOrEqual(member, constant),
                    };

                case ScopeOperator.InSelect or ScopeOperator.NotInSelect:
                    var select = condition.RequireSelect();
                    var indexType = indexTypes?.Invoke(select.Table) ?? FindIndexType(select.Table);
                    var inner = Expression.Parameter(indexType, "inner");
                    var selectedProperty = indexType.GetProperty(select.Column, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                        ?? throw new InvalidOperationException($"Index '{indexType.Name}' has no column '{select.Column}'.");
                    var selector = Expression.Lambda(
                        typeof(Func<,>).MakeGenericType(indexType, typeof(object)),
                        Expression.Convert(Expression.Property(inner, selectedProperty), typeof(object)),
                        inner);
                    var where = Expression.Lambda(
                        typeof(Func<,>).MakeGenericType(indexType, typeof(bool)),
                        Translate(select.Filter, inner, indexTypes),
                        inner);
                    var method = (condition.Operator == ScopeOperator.InSelect ? IsInSelectMethod : IsNotInSelectMethod).MakeGenericMethod(indexType);
                    return Expression.Call(method, Expression.Convert(member, typeof(object)), Expression.Quote(selector), Expression.Quote(where));

                default:
                    var single = condition.Values.Count > 0 ? condition.Values[0] : null;
                    var typed = single is null ? null : Convert(single, elementType);
                    return Expression.Equal(member, Expression.Constant(typed, property.PropertyType));
            }
        }

        if (filter.All is { Count: > 0 } all)
        {
            return all.Select(part => Translate(part, parameter, indexTypes)).Aggregate(Expression.AndAlso);
        }

        if (filter.Any is { Count: > 0 } any)
        {
            return any.Select(part => Translate(part, parameter, indexTypes)).Aggregate(Expression.OrElse);
        }

        return Expression.Constant(false);
    }

    private static object Convert(object value, Type type)
    {
        if (type.IsInstanceOfType(value))
        {
            return value;
        }

        if (type == typeof(DateTime) && value is DateTimeOffset offset)
        {
            return offset.UtcDateTime;
        }

        return System.Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>The one loaded YesSql index type whose name is <paramref name="table"/>. Only a
    /// match is cached: an assembly loaded later is still found.</summary>
    private static Type FindIndexType(string table)
    {
        if (IndexTypes.TryGetValue(table, out var known))
        {
            return known;
        }

        var matches = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(LoadableTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IIndex).IsAssignableFrom(type) && string.Equals(type.Name, table, StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();

        return matches.Count == 1
            ? IndexTypes.GetOrAdd(table, matches[0])
            : throw new InvalidOperationException($"No single YesSql index type is named '{table}'; pass its type to compile the semi-join.");
    }

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }
}
