namespace Grove.Display.Components.Primitives;

/// <summary>
/// Specifies the ways a <see cref="Grove.Display.Components.Primitives.GroveTabs" /> component renders its items.
/// </summary>
public enum TabRenderMode
{
    /// <summary>
    /// The GroveTabs component switches its items server side. Only the selected item is rendered.
    /// </summary>
    Server,

    /// <summary>
    /// The GroveTabs components switches its items client-side. All items are rendered and the unselected ones are hidden with CSS.
    /// </summary>
    Client
}

