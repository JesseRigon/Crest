#nullable enable
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Access;
using Grove.Data.Queries.Structured;
using YesSql;

namespace Grove.Data.Queries.Structured;

/// <summary>A compiled statement: parameterised SQL for one dialect, the values to bind, the output columns.</summary>
public sealed record CompiledStatement(
    string Sql,
    string? CountSql,
    IReadOnlyDictionary<string, object> Parameters,
    IReadOnlyList<QueryColumn> Columns,
    int PageSize,
    int Offset,
    /// <summary>True when a scope rule admits no row: nothing runs.</summary>
    bool IsEmpty,
    /// <summary>The output column that carries the From table's document id, when projected.</summary>
    string? DocumentIdColumn);

/// <summary>
/// Compiles a <see cref="QueryPlan"/> through the store's dialect: the scope rule of every
/// table conjoined into the WHERE (into the ON clause for a left-joined table), every value bound as a parameter, a stable order added when
/// the plan has none, paging written by the dialect.
/// </summary>
public static class StructuredQueryCompiler
{
    private const string SkipParameter = "__skip";
    private const string TakeParameter = "__take";

    /// <summary>The page size of a query with no Page step.</summary>
    public const int DefaultPageSize = 100;

    /// <summary>The largest page a request may ask for.</summary>
    public const int MaxPageSize = 500;

    /// <summary>The page a run reads: the request's size, else the query's, else the default; never past the cap.</summary>
    public static int PageSize(int? requested, int? querySize = null) =>
        Math.Min(requested is > 0 ? requested.Value : querySize ?? DefaultPageSize, MaxPageSize);

    public static CompiledStatement Compile(
        QueryPlan plan,
        ISqlDialect dialect,
        string tablePrefix,
        string schema,
        ScopeSet scopes,
        IDictionary<string, object>? boundParameters,
        string? pageToken,
        int? pageSizeOverride)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(dialect);
        ArgumentNullException.ThrowIfNull(scopes);

        var parameters = new Dictionary<string, object>();
        var builder = dialect.CreateBuilder(tablePrefix);
        builder.Select();

        var isEmpty = false;
        var selects = new SelectAliases();

        // Scope is a step: every table of the plan is narrowed to what the caller may see. The
        // joins are written here (not by the builder's Join) so a left-joined table's predicate
        // goes into its ON clause.
        foreach (var table in plan.Tables)
        {
            var rule = scopes.Require(table.Table.Name);

            if (table.ContentType is not null && scopes.TryGet(table.ContentType, out var typeRule))
            {
                rule = rule.And(typeRule);
            }

            var predicate = rule.Kind switch
            {
                ScopeKind.None => "1 = 0",
                ScopeKind.Filter => Render(rule.Filter!, table.Alias, builder, dialect, schema, parameters, selects),
                _ => null,
            };

            if (table.Join is null)
            {
                builder.Table(table.Table.Name, table.Alias, schema);
            }
            else
            {
                var isLeft = table.Join == JoinKind.Left;
                var on = $"{Column(builder, table.JoinToColumn!.Alias, table.JoinToColumn.Column.Name, schema)} = {Column(builder, table.Alias, table.JoinColumn!.Column.Name, schema)}";

                // The right side of a left join keeps its unmatched rows: an out-of-scope match
                // becomes a null row, never a row the caller may not see.
                if (isLeft && predicate is not null)
                {
                    // A rendered filter is a single comparison or parenthesised: AND-safe as is.
                    on += " AND " + predicate;
                    predicate = null;
                }

                builder.From($" {(isLeft ? "LEFT" : "INNER")} JOIN {builder.FormatTable(table.Table.Name, schema)} AS {dialect.QuoteForAliasName(table.Alias)} ON {on}");
            }

            if (rule.Kind == ScopeKind.None && predicate is not null)
            {
                isEmpty = true;
            }
            else if (predicate is not null)
            {
                builder.WhereAnd(predicate);
            }
        }

        if (plan.From.ContentType is not null)
        {
            builder.WhereAnd($"{Column(builder, plan.From.Alias, "ContentType", schema)} = {Bind(parameters, plan.From.ContentType)}");
            builder.WhereAnd($"{Column(builder, plan.From.Alias, "Published", schema)} = {Bind(parameters, true)}");
        }

        foreach (var filter in plan.Filters)
        {
            builder.WhereAnd(Render(filter, builder, dialect, schema, parameters, boundParameters));
        }

        var countBuilder = builder.Clone();
        countBuilder.Selector("COUNT(*)");

        builder.Selector(string.Join(", ", plan.Columns.Select(column =>
            $"{Column(builder, column.Source.Alias, column.Source.Column.Name, schema)} AS {dialect.QuoteForAliasName(column.Name)}")));

        var ordered = false;

        foreach (var sort in plan.Sorts)
        {
            var column = Column(builder, sort.Column.Alias, sort.Column.Column.Name, schema);

            if (!ordered)
            {
                if (sort.Descending) builder.OrderByDescending(column); else builder.OrderBy(column);
                ordered = true;
            }
            else
            {
                if (sort.Descending) builder.ThenOrderByDescending(column); else builder.ThenOrderBy(column);
            }
        }

        // A stable order, so pages never repeat or skip rows.
        var stable = plan.From.StableColumn;

        if (!plan.Sorts.Any(sort => sort.Column.Alias == plan.From.Alias && string.Equals(sort.Column.Column.Name, stable.Name, StringComparison.OrdinalIgnoreCase)))
        {
            var column = Column(builder, plan.From.Alias, stable.Name, schema);

            if (ordered) builder.ThenOrderBy(column); else builder.OrderBy(column);
        }

        // Every run is paged: the query's own size, else the default; a request may narrow it, never past the cap.
        var pageSize = PageSize(pageSizeOverride, plan.PageSize);
        var offset = PageTokens.Decode(pageToken ?? plan.PageToken);
        parameters[SkipParameter] = offset;
        parameters[TakeParameter] = pageSize;
        builder.Skip("@" + SkipParameter);
        builder.Take("@" + TakeParameter);

        var documentIdColumn = plan.Columns.FirstOrDefault(column =>
            column.Source.Alias == plan.From.Alias && string.Equals(column.Source.Column.Name, "DocumentId", StringComparison.OrdinalIgnoreCase))?.Name;

        return new CompiledStatement(
            builder.ToSqlString(),
            countBuilder.ToSqlString(),
            parameters,
            plan.OutputColumns,
            pageSize,
            offset,
            isEmpty,
            documentIdColumn);
    }

    private static string Column(ISqlBuilder builder, string alias, string column, string schema) =>
        builder.FormatColumn(alias, column, schema, isAlias: true);

    private static string Bind(Dictionary<string, object> parameters, object value)
    {
        var name = "p" + parameters.Count.ToString(CultureInfo.InvariantCulture);
        parameters[name] = value;

        return "@" + name;
    }

    /// <summary>Names the tables a scope's semi-joins select from, one alias each per statement.</summary>
    private sealed class SelectAliases
    {
        private int _next;

        public string Next() => "__scope_select" + (_next++).ToString(CultureInfo.InvariantCulture);
    }

    private static string Render(ScopeFilter filter, string alias, ISqlBuilder builder, ISqlDialect dialect, string schema, Dictionary<string, object> parameters, SelectAliases selects)
    {
        if (filter.Condition is { } condition)
        {
            var column = Column(builder, alias, condition.Column, schema);

            switch (condition.Operator)
            {
                case ScopeOperator.Equals:
                    var value = condition.Values.Count > 0 ? condition.Values[0] : null;

                    return value is null ? $"{column} IS NULL" : $"{column} = {Bind(parameters, value)}";

                case ScopeOperator.IsNull:
                    return $"{column} IS NULL";

                case ScopeOperator.LessThan:
                case ScopeOperator.LessThanOrEqual:
                case ScopeOperator.GreaterThan:
                case ScopeOperator.GreaterThanOrEqual:
                    var operand = condition.Values.Count > 0 ? condition.Values[0] : null;

                    // A comparison with null admits nothing, as in SQL.
                    return operand is null ? "1 = 0" : $"{column} {condition.ComparisonSql} {Bind(parameters, ScopeValue(operand))}";

                case ScopeOperator.InSelect:
                case ScopeOperator.NotInSelect:
                    var select = condition.RequireSelect();
                    var inner = selects.Next();
                    var where = Render(select.Filter, inner, builder, dialect, schema, parameters, selects);

                    return $"{column} {(condition.Operator == ScopeOperator.NotInSelect ? "NOT IN" : "IN")} (SELECT {Column(builder, inner, select.Column, schema)} FROM {builder.FormatTable(select.Table, schema)} AS {dialect.QuoteForAliasName(inner)} WHERE {where})";

                case ScopeOperator.In:
                case ScopeOperator.NotIn:
                    var negated = condition.Operator == ScopeOperator.NotIn;
                    var values = condition.Values.Where(candidate => candidate is not null).Select(candidate => Bind(parameters, candidate!)).ToArray();
                    var hasNull = values.Length != condition.Values.Count;
                    var members = new List<string>();

                    if (values.Length > 0)
                    {
                        members.Add(column + InList(negated, values));
                    }

                    if (hasNull)
                    {
                        members.Add(column + (negated ? " IS NOT NULL" : " IS NULL"));
                    }

                    return members.Count switch
                    {
                        0 => negated ? "1 = 1" : "1 = 0",
                        1 => members[0],
                        _ => "(" + string.Join(negated ? " AND " : " OR ", members) + ")",
                    };

                default:
                    throw new NotSupportedException($"Unsupported scope operator '{condition.Operator}'.");
            }
        }

        if (filter.All is { Count: > 0 } all)
        {
            return "(" + string.Join(" AND ", all.Select(child => Render(child, alias, builder, dialect, schema, parameters, selects))) + ")";
        }

        if (filter.Any is { Count: > 0 } any)
        {
            return "(" + string.Join(" OR ", any.Select(child => Render(child, alias, builder, dialect, schema, parameters, selects))) + ")";
        }

        return "1 = 0";
    }

    /// <summary>
    /// " IN (@p0, @p1)" over bound scalars. Not the dialect's operator: YesSql writes a single
    /// "@p" as " IN @p" for Dapper's list expansion, which is a syntax error for a scalar.
    /// </summary>
    private static string InList(bool negated, IEnumerable<string> members) =>
        $" {(negated ? "NOT IN" : "IN")} ({string.Join(", ", members)})";

    /// <summary>A scope value as bound: an instant as a UTC date and time, as YesSql stores one.</summary>
    internal static object ScopeValue(object value) => value is DateTimeOffset offset ? offset.UtcDateTime : value;

    private static string Render(PlanFilter filter, ISqlBuilder builder, ISqlDialect dialect, string schema, Dictionary<string, object> parameters, IDictionary<string, object>? boundParameters)
    {
        var column = Column(builder, filter.Column.Alias, filter.Column.Column.Name, schema);

        switch (filter.Operator)
        {
            case FilterOperator.IsNull:
                return $"{column} IS NULL";

            case FilterOperator.IsNotNull:
                return $"{column} IS NOT NULL";
        }

        var value = filter.Parameter is not null
            ? ParameterValue(filter.Parameter, boundParameters)
            : ConvertValue(filter.Value, filter.Operator == FilterOperator.In ? typeof(string[]) : filter.Column.Column.Type, filter.Column.Column.Name);

        if (filter.Operator == FilterOperator.In)
        {
            var values = value as IEnumerable<object?> ?? (value as System.Collections.IEnumerable)?.Cast<object?>() ?? [value];
            var members = values.Where(member => member is not null).Select(member => Bind(parameters, member!)).ToArray();

            return members.Length == 0 ? "1 = 0" : column + InList(negated: false, members);
        }

        if (value is null)
        {
            return filter.Operator == FilterOperator.NotEquals ? $"{column} IS NOT NULL" : $"{column} IS NULL";
        }

        var op = filter.Operator switch
        {
            FilterOperator.Equals => "=",
            FilterOperator.NotEquals => "<>",
            FilterOperator.GreaterThan => ">",
            FilterOperator.GreaterThanOrEqual => ">=",
            FilterOperator.LessThan => "<",
            FilterOperator.LessThanOrEqual => "<=",
            FilterOperator.Like => "LIKE",
            _ => throw new NotSupportedException($"Unsupported filter operator '{filter.Operator}'."),
        };

        return $"{column} {op} {Bind(parameters, value)}";
    }

    private static object? ParameterValue(QueryParameter parameter, IDictionary<string, object>? boundParameters)
    {
        object? value = null;

        if (boundParameters is not null)
        {
            var match = boundParameters.FirstOrDefault(pair => string.Equals(pair.Key, parameter.Name, StringComparison.OrdinalIgnoreCase));
            value = match.Key is null ? null : match.Value;
        }

        if (value is null && parameter.Default is not null)
        {
            value = parameter.Default;
        }

        if (value is null)
        {
            if (parameter.Required)
            {
                throw new StructuredQueryException($"The parameter '{parameter.Name}' is required.");
            }

            return null;
        }

        return ConvertValue(value, parameter.ClrType, parameter.Name);
    }

    /// <summary>Converts a bound value (a CLR value, a JSON node or element) to the target CLR type; never a string spliced into SQL.</summary>
    public static object? ConvertValue(object? value, Type targetType, string name)
    {
        if (value is null)
        {
            return null;
        }

        if (value is JsonNode node)
        {
            value = node switch
            {
                JsonArray array => array.Select(element => ConvertValue(element, typeof(string), name)).ToArray(),
                JsonValue jsonValue => jsonValue.GetValue<object>(),
                _ => throw new StructuredQueryException($"The value of '{name}' is an object; a scalar or array is expected."),
            };
        }

        if (value is JsonElement element)
        {
            value = element.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDecimal(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Array => element.EnumerateArray().Select(member => ConvertValue(member, typeof(string), name)).ToArray(),
                _ => throw new StructuredQueryException($"The value of '{name}' is an object; a scalar or array is expected."),
            };
        }

        if (value is null)
        {
            return null;
        }

        var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (type == typeof(string[]))
        {
            return value is System.Collections.IEnumerable enumerable and not string
                ? enumerable.Cast<object?>().Select(member => member?.ToString()).ToArray()
                : new[] { value.ToString() };
        }

        if (type.IsInstanceOfType(value))
        {
            return value;
        }

        try
        {
            if (type == typeof(DateTime))
            {
                return DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            }

            if (type.IsEnum)
            {
                return Enum.Parse(type, value.ToString()!, ignoreCase: true);
            }

            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            throw new StructuredQueryException($"The value of '{name}' is not a {type.Name}.");
        }
    }
}

/// <summary>An opaque page token: the offset of the page, encoded.</summary>
public static class PageTokens
{
    public static string Encode(int offset) => Convert.ToBase64String(Encoding.ASCII.GetBytes(offset.ToString(CultureInfo.InvariantCulture)));

    public static int Decode(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return 0;
        }

        try
        {
            var offset = int.Parse(Encoding.ASCII.GetString(Convert.FromBase64String(token)), CultureInfo.InvariantCulture);

            return offset < 0 ? throw new StructuredQueryException("Invalid page token.") : offset;
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or OverflowException)
        {
            throw new StructuredQueryException("Invalid page token.");
        }
    }
}
