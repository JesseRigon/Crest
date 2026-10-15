namespace Grove.Display.Components.Primitives;

/// <summary>
/// Specifies the severity of a <see cref="Grove.Display.Components.Primitives.GroveNotification" />. Severity changes the visual styling of the GroveNotification (icon and background color).
/// </summary>
public enum NotificationSeverity
{
    /// <summary>
    /// Represents an error.
    /// </summary>
    Error,

    /// <summary>
    /// Represents some generic information.
    /// </summary>
    Info,

    /// <summary>
    /// Represents a success.
    /// </summary>
    Success,

    /// <summary>
    /// Represents a warning.
    /// </summary>
    Warning
}

