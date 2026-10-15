using System;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// Specifies the position at which a Grove Blazor component renders its built-in <see cref="Grove.Display.Components.Primitives.GrovePager" />.
/// </summary>
[Flags]
public enum PagerPosition
{
    /// <summary>
    /// GrovePager is displayed at the top of the component.
    /// </summary>
    Top = 1,

    /// <summary>
    /// GrovePager is displayed at the bottom of the component.
    /// </summary>
    Bottom = 2,

    /// <summary>
    /// GrovePager is displayed at the top and at the bottom of the component.
    /// </summary>
    TopAndBottom = Top | Bottom
}

