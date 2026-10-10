namespace Crest.Access;

public enum ScopeOperator
{
    Equals,
    In,
    NotIn,
}

/// <summary>One condition on one column of the scoped target.</summary>
public sealed record ScopeCondition(string Column, ScopeOperator Operator, IReadOnlyList<object?> Values)
{
    public static ScopeCondition Equal(string column, object? value) => new(column, ScopeOperator.Equals, [value]);

    public static ScopeCondition In(string column, IEnumerable<object?> values) => new(column, ScopeOperator.In, values.ToArray());

    public static ScopeCondition NotIn(string column, IEnumerable<object?> values) => new(column, ScopeOperator.NotIn, values.ToArray());
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

    /// <summary>Evaluates the filter in memory against a row reader.</summary>
    public bool Matches(Func<string, object?> column)
    {
        if (Condition is { } condition)
        {
            var value = column(condition.Column);
            return condition.Operator switch
            {
                ScopeOperator.Equals => ScopeValues.Equal(value, condition.Values.Count > 0 ? condition.Values[0] : null),
                ScopeOperator.In => condition.Values.Any(candidate => ScopeValues.Equal(value, candidate)),
                ScopeOperator.NotIn => !condition.Values.Any(candidate => ScopeValues.Equal(value, candidate)),
                _ => false,
            };
        }

        if (All is { } all)
        {
            return all.All(filter => filter.Matches(column));
        }

        if (Any is { } any)
        {
            return any.Any(filter => filter.Matches(column));
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
