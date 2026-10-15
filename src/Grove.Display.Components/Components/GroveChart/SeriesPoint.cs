namespace Grove.Display.Components.Primitives;

/// <summary>
/// Represents a data item in a <see cref="Grove.Display.Components.Primitives.GroveChart" />.
/// </summary>
public class SeriesPoint
{
    /// <summary>
    /// Gets the category axis value.
    /// </summary>
    public double Category { get; set; }

    /// <summary>
    /// Gets the value axis value.
    /// </summary>
    public double Value { get; set; }
}

