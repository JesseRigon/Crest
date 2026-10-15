namespace Grove.Access;

public enum ScopeOperator
{
    Equals,
    In,
    NotIn,

    /// <summary>The column is less than the value (time bounds: an end not yet passed is
    /// <see cref="GreaterThan"/> now).</summary>
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,

    /// <summary>The column is null (an open time bound). Takes no value.</summary>
    IsNull,

    /// <summary>A semi-join: the column's value is in the set <see cref="ScopeCondition.Select"/>
    /// selects from another index table. Compiled into SQL only; refused in memory.</summary>
    InSelect,

    /// <summary>The column's value is not in the set <see cref="ScopeCondition.Select"/> selects.
    /// A null column is not admitted (SQL's <c>NOT IN</c>). Compiled into SQL only.</summary>
    NotInSelect,
}

/// <summary>
/// The set side of a semi-join: <c>SELECT <see cref="Column"/> FROM <see cref="Table"/> WHERE
/// <see cref="Filter"/></c>. The table is an index table name without the tenant's prefix; the
/// filter is over that table's own columns and may itself hold semi-joins. The selected table is
/// read as part of the scope, not as a target: it needs no scope provider of its own, and the
/// selected column must never be null.
/// </summary>
public sealed record ScopeSelect(string Table, string Column, ScopeFilter Filter);

/// <summary>One condition on one column of the scoped target. Comparisons and
/// <see cref="ScopeOperator.Equals"/> take one value, <see cref="ScopeOperator.In"/> and
/// <see cref="ScopeOperator.NotIn"/> a list, <see cref="ScopeOperator.IsNull"/> none, and the
/// semi-joins a <see cref="Select"/>.</summary>
public sealed record ScopeCondition(string Column, ScopeOperator Operator, IReadOnlyList<object?> Values, ScopeSelect? Select = null)
{
    public static ScopeCondition Equal(string column, object? value) => new(column, ScopeOperator.Equals, [value]);

    public static ScopeCondition In(string column, IEnumerable<object?> values) => new(column, ScopeOperator.In, values.ToArray());

    public static ScopeCondition NotIn(string column, IEnumerable<object?> values) => new(column, ScopeOperator.NotIn, values.ToArray());

    public static ScopeCondition LessThan(string column, object value) => new(column, ScopeOperator.LessThan, [value]);

    public static ScopeCondition LessThanOrEqual(string column, object value) => new(column, ScopeOperator.LessThanOrEqual, [value]);

    public static ScopeCondition GreaterThan(string column, object value) => new(column, ScopeOperator.GreaterThan, [value]);

    public static ScopeCondition GreaterThanOrEqual(string column, object value) => new(column, ScopeOperator.GreaterThanOrEqual, [value]);

    public static ScopeCondition IsNull(string column) => new(column, ScopeOperator.IsNull, []);

    /// <summary><paramref name="column"/> IN (SELECT <paramref name="selected"/> FROM
    /// <paramref name="table"/> WHERE <paramref name="where"/>).</summary>
    public static ScopeCondition InSelect(string column, string table, string selected, ScopeFilter where) =>
        new(column, ScopeOperator.InSelect, [], new ScopeSelect(table, selected, where));

    /// <summary><paramref name="column"/> NOT IN (SELECT <paramref name="selected"/> FROM
    /// <paramref name="table"/> WHERE <paramref name="where"/>).</summary>
    public static ScopeCondition NotInSelect(string column, string table, string selected, ScopeFilter where) =>
        new(column, ScopeOperator.NotInSelect, [], new ScopeSelect(table, selected, where));

    /// <summary>The comparison's SQL operator, or null when the operator is not a comparison.</summary>
    public string? ComparisonSql => Operator switch
    {
        ScopeOperator.LessThan => "<",
        ScopeOperator.LessThanOrEqual => "<=",
        ScopeOperator.GreaterThan => ">",
        ScopeOperator.GreaterThanOrEqual => ">=",
        _ => null,
    };

    /// <summary>The selected set, or throws when a semi-join has none.</summary>
    public ScopeSelect RequireSelect() =>
        Select ?? throw new InvalidOperationException($"The semi-join on '{Column}' has no select.");
}

/// <summary>
/// A boolean filter over a target's columns, as data: compiled into a SQL predicate by the
/// query pipeline, into a YesSql expression by the content lists, or evaluated in memory by
/// sources that are not SQL. Exactly one of <see cref="Condition"/>, <see cref="All"/>,
/// <see cref="Any"/> is set.
/// </summary>
public sealed class ScopeFilter
{
    public ScopeCondition? Condition { get; init; }

    public IReadOnlyList<ScopeFilter>? All { get; init; }

    public IReadOnlyList<ScopeFilter>? Any { get; init; }

    public static ScopeFilter Of(ScopeCondition condition) => new() { Condition = condition };

    public static ScopeFilter AllOf(params ScopeFilter[] filters) => new() { All = filters };

    public static ScopeFilter AnyOf(params ScopeFilter[] filters) => new() { Any = filters };

    /// <summary>True when the filter (or a part of it) is a semi-join, which only a store can
    /// evaluate.</summary>
    public bool HasSelect =>
        Condition is { Operator: ScopeOperator.InSelect or ScopeOperator.NotInSelect }
        || (All?.Any(filter => filter.HasSelect) ?? false)
        || (Any?.Any(filter => filter.HasSelect) ?? false);

    /// <summary>
    /// Evaluates the filter in memory against a row reader. A semi-join cannot be evaluated
    /// without the other table's rows, so a filter that holds one throws
    /// <see cref="NotSupportedException"/> (whether or not the semi-join would be reached): an
    /// in-memory source must refuse such a scope, never guess it. Check <see cref="HasSelect"/>
    /// first to refuse up front. A comparison with a null side admits nothing, as in SQL.
    /// </summary>
    public bool Matches(Func<string, object?> column)
    {
        if (HasSelect)
        {
            throw new NotSupportedException("A scope with a semi-join (InSelect, NotInSelect) is evaluated by the store only, never in memory.");
        }

        return Evaluate(column);
    }

    private bool Evaluate(Func<string, object?> column)
    {
        if (Condition is { } condition)
        {
            var value = column(condition.Column);
            var operand = condition.Values.Count > 0 ? condition.Values[0] : null;
            return condition.Operator switch
            {
                ScopeOperator.Equals => ScopeValues.Equal(value, operand),
                ScopeOperator.In => condition.Values.Any(candidate => ScopeValues.Equal(value, candidate)),
                ScopeOperator.NotIn => !condition.Values.Any(candidate => ScopeValues.Equal(value, candidate)),
                ScopeOperator.IsNull => value is null,
                ScopeOperator.LessThan => ScopeValues.Compare(value, operand) is < 0,
                ScopeOperator.LessThanOrEqual => ScopeValues.Compare(value, operand) is <= 0,
                ScopeOperator.GreaterThan => ScopeValues.Compare(value, operand) is > 0,
                ScopeOperator.GreaterThanOrEqual => ScopeValues.Compare(value, operand) is >= 0,
                _ => false,
            };
        }

        if (All is { } all)
        {
            return all.All(filter => filter.Evaluate(column));
        }

        if (Any is { } any)
        {
            return any.Any(filter => filter.Evaluate(column));
        }

        return false;
    }
}

internal static class ScopeValues
{
    public static bool Equal(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left is string l && right is string r)
        {
            return string.Equals(l, r, StringComparison.Ordinal);
        }

        return Equals(left, right) || string.Equals(left.ToString(), right.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Orders two values; null when either is null or they cannot be compared. Dates
    /// compare as instants (a <see cref="DateTimeOffset"/> with a UTC <see cref="DateTime"/>);
    /// numbers of different types compare as decimals.</summary>
    public static int? Compare(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return null;
        }

        if (left is DateTimeOffset || right is DateTimeOffset || left is DateTime || right is DateTime)
        {
            return Instant(left) is { } l && Instant(right) is { } r ? l.CompareTo(r) : null;
        }

        if (left is string ls && right is string rs)
        {
            return string.CompareOrdinal(ls, rs);
        }

        if (left.GetType() == right.GetType() && left is IComparable comparable)
        {
            return comparable.CompareTo(right);
        }

        try
        {
            return Convert.ToDecimal(left, System.Globalization.CultureInfo.InvariantCulture)
                .CompareTo(Convert.ToDecimal(right, System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            return null;
        }
    }

    private static DateTimeOffset? Instant(object value) => value switch
    {
        DateTimeOffset offset => offset,
        DateTime { Kind: DateTimeKind.Local } local => new DateTimeOffset(local),
        DateTime time => new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)),
        _ => null,
    };
}

public enum ScopeKind
{
    /// <summary>Every row of the target.</summary>
    All,
    /// <summary>No row of the target.</summary>
    None,
    /// <summary>The rows the filter admits.</summary>
    Filter,
}

/// <summary>What a caller may see of one target (an index table or a content type).</summary>
public sealed class ScopeRule
{
    public ScopeKind Kind { get; init; }

    public ScopeFilter? Filter { get; init; }

    public static readonly ScopeRule All = new() { Kind = ScopeKind.All };

    public static readonly ScopeRule None = new() { Kind = ScopeKind.None };

    public static ScopeRule Where(ScopeFilter filter) => new() { Kind = ScopeKind.Filter, Filter = filter };

    /// <summary>Narrows this rule by another: both must admit the row.</summary>
    public ScopeRule And(ScopeRule other)
    {
        if (Kind == ScopeKind.None || other.Kind == ScopeKind.None)
        {
            return None;
        }

        if (Kind == ScopeKind.All)
        {
            return other;
        }

        if (other.Kind == ScopeKind.All)
        {
            return this;
        }

        return Where(ScopeFilter.AllOf(Filter!, other.Filter!));
    }

    public bool Admits(Func<string, object?> column) => Kind switch
    {
        ScopeKind.All => true,
        ScopeKind.None => false,
        _ => Filter!.Matches(column),
    };
}

/// <summary>
/// Registered per target by the module that owns it: the predicate that limits the
/// target's rows to what a caller may see. Several providers for one target are
/// conjoined. A target with no provider is refused, never served unfiltered.
/// </summary>
public interface IScopeProvider
{
    /// <summary>The target: an index table name (<c>ContentItemIndex</c>) or a content
    /// type name. Targets are compared ordinally, ignoring case.</summary>
    string Target { get; }

    /// <summary>Bumped when the provider's logic changes, so compiled scope plans are
    /// rebuilt.</summary>
    int Version { get; }

    Task<ScopeRule> BuildAsync(CallerContext caller, CancellationToken cancellationToken = default);
}

/// <summary>
/// Scope providers whose targets are known only at run time: a domain that registers one
/// provider per registered item kind (sharing, for each shareable kind with an index table).
/// The scope set conjoins them with the <see cref="IScopeProvider"/>s registered directly.
/// </summary>
public interface IScopeProviderSource
{
    IEnumerable<IScopeProvider> GetProviders();
}

/// <summary>
/// Every target's rule for one caller, compiled once per scope signature and provider
/// versions. A target absent from the set has no provider and is refused.
/// </summary>
public sealed class ScopeSet
{
    private readonly IReadOnlyDictionary<string, ScopeRule> _rules;

    public ScopeSet(string signature, IReadOnlyDictionary<string, ScopeRule> rules)
    {
        Signature = signature;
        _rules = rules;
    }

    public string Signature { get; }

    public IEnumerable<string> Targets => _rules.Keys;

    public bool TryGet(string target, out ScopeRule rule) => _rules.TryGetValue(target, out rule!);

    /// <summary>The rule for a target, or throws <see cref="ScopeRefusedException"/> when the
    /// target has no provider.</summary>
    public ScopeRule Require(string target) =>
        _rules.TryGetValue(target, out var rule) ? rule : throw new ScopeRefusedException(target);
}

public sealed class ScopeRefusedException(string target)
    : InvalidOperationException($"No scope provider is registered for '{target}'; it cannot be read unfiltered.")
{
    public string Target { get; } = target;
}

/// <summary>Builds and caches a caller's <see cref="ScopeSet"/>.</summary>
public interface IScopeSetProvider
{
    Task<ScopeSet> GetAsync(CallerContext caller, CancellationToken cancellationToken = default);
}

/// <summary>
/// What a pipeline executes with: the caller and their scope. Sources and data activities
/// receive this, never a raw session.
/// </summary>
public sealed record ScopedExecution(CallerContext Caller, ScopeSet Scopes);
