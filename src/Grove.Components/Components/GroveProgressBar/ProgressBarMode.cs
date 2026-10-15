namespace Grove.Components.Primitives;

/// <summary>
/// Specifies the behavior of <see cref="Grove.Components.Primitives.GroveProgressBar" /> or <see cref="Grove.Components.Primitives.GroveProgressBarCircular" />.
/// </summary>
public enum ProgressBarMode
{
    /// <summary>
    /// GroveProgressBar displays its value as a percentage range (0 to 100).
    /// </summary>
    Determinate,

    /// <summary>
    /// GroveProgressBar displays continuous animation.
    /// </summary>
    Indeterminate
}

