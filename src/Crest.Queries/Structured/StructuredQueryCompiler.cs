#nullable enable
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Access;
using Crest.Queries.Structured;
using YesSql;

namespace Crest.Queries.Structured;

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
/// table conjoined into the WHERE, every value bound as a parameter, a stable order added when
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
        builder.Table(plan.From.Table.Name, plan.From.Alias, schema);

        var isEmpty = false;
        var leftScoped = new List<string>();

        foreach (var table in plan.Tables.Skip(1))
        {
            builder.Join(
                table.Join == JoinKind.Left ? JoinType.Left : JoinType.Inner,
                table.Table.Name,
                table.JoinToColumn!.Alias,
                table.JoinToColumn.Column.Name,
                table.Table.Name,
                table.JoinColumn!.Column.Name,
                schema,
                table.Alias,
                table.Alias);
        }

        // Scope is a step: every table of the plan is narrowed to what the caller may see.
        foreach (var table in plan.Tables)
        {
            var rule = scopes.Require(table.Table.Name);

            if (table.ContentType is not null && scopes.TryGet(table.ContentType, out var typeRule))
            {
                rule = rule.And(typeRule);
            }

            switch (rule.Kind)
            {
                case ScopeKind.None:
                    isEmpty = true;
                    break;

                case ScopeKind.Filter:
                    var predicate = Render(rule.Filter!, table.Alias, builder, dialect, schema, parameters);

                    // The right side of a left join keeps its unmatched rows; an out-of-scope
                    // match drops the row rather than reaching the caller.
                    builder.WhereAnd(table.Join == JoinKind.Left
                        ? $"({predicate} OR {Column(builder, table.Alias, table.StableColumn.Name, schema)} IS NULL)"
                        : predicate);
                    break;
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
        var pageSize = Math.Min(pageSizeOverride is > 0 ? pageSizeOverride.Value : plan.PageSize ?? DefaultPageSize, MaxPageSize);
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

    private static string Render(ScopeFilter filter, string alias, ISqlBuilder builder, ISqlDialect dialect, string schema, Dictionary<string, object> parameters)
    {
        if (filter.Condition is { } condition)
        {
            var column = Column(builder, alias, condition.Column, schema);

            switch (condition.Operator)
            {
                case ScopeOperator.Equals:
                    var value = condition.Values.Count > 0 ? condition.Values[0] : null;

                    return value is null ? $"{column} IS NULL" : $"{column} = {Bind(parameters, value)}";

                case ScopeOperator.In:
                case ScopeOperator.NotIn:
                    var negated = condition.Operator == ScopeOperator.NotIn;
                    var values = condition.Values.Where(candidate => candidate is not null).Select(candidate => Bind(parameters, candidate!)).ToArray();
                    var hasNull = values.Length != condition.Values.Count;
                    var members = new List<string>();

                    if (values.Length > 0)
                    {
                        members.Add(column + (negated ? dialect.NotInOperator(string.Join(", ", values)) : dialect.InOperator(string.Join(", ", values))));
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
            return "(" + string.Join(" AND ", all.Select(child => Render(child, alias, builder, dialect, schema, parameters))) + ")";
        }

        if (filter.Any is { Count: > 0 } any)
        {
            return "(" + string.Join(" OR ", any.Select(child => Render(child, alias, builder, dialect, schema, parameters))) + ")";
        }

        return "1 = 0";
    }

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

            return members.Length == 0 ? "1 = 0" : column + dialect.InOperator(string.Join(", ", members));
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
