namespace Grove.Display.Components.Primitives;

/// <summary>
/// Specifies the behavior of <see cref="Grove.Display.Components.Primitives.GroveProgressBar" /> or <see cref="Grove.Display.Components.Primitives.GroveProgressBarCircular" />.
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

