namespace Grove.Components.Primitives;

/// <summary>
/// Specifies the display style of a <see cref="Grove.Components.Primitives.GroveProgressBar" /> and <see cref="Grove.Components.Primitives.GroveProgressBarCircular" />. Affects the visual styling of GroveProgressBar (background and text color) and GroveProgressBarCircular (stroke and text color).
/// </summary>
public enum ProgressBarStyle
{
    /// <summary>
    /// Primary styling. Similar to primary buttons.
    /// </summary>
    Primary,

    /// <summary>
    /// Secondary styling. Similar to secondary buttons.
    /// </summary>
    Secondary,

    /// <summary>
    /// Light styling. Similar to light buttons.
    /// </summary>
    Light,

    /// <summary>
    /// Base styling. Similar to base buttons.
    /// </summary>
    Base,

    /// <summary>
    /// Dark styling. Similar to dark buttons.
    /// </summary>
    Dark,

    /// <summary>
    /// Success styling.
    /// </summary>
    Success,

    /// <summary>
    /// Danger styling.
    /// </summary>
    Danger,

    /// <summary>
    /// Warning styling.
    /// </summary>
    Warning,

    /// <summary>
    /// Informative styling.
    /// </summary>
    Info
}

