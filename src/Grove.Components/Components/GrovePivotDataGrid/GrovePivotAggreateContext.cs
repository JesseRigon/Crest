using System.Linq;
using Grove.Components.Primitives;

namespace Grove.Components.Primitives;

/// <summary>
/// GrovePivotAggreateContext.
/// </summary>
public class GrovePivotAggreateContext<T>
{
    /// <summary>
    /// Gets the query.
    /// </summary>
    public IQueryable<T>? View { get; internal set; }

    /// <summary>
    /// Gets the aggregate.
    /// </summary>
    public GrovePivotAggregate<T>? Aggregate { get; internal set; }

    /// <summary>
    /// Gets the aggregate value.
    /// </summary>
    public object? Value { get; internal set; }

    /// <summary>
    /// Gets the row index.
    /// </summary>
    public int? Index { get; internal set; }
}

