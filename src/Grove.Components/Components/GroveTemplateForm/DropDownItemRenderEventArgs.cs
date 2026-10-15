using Grove.Components.Primitives;

namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about GroveDropDown ItemRender event.
/// </summary>
public class DropDownItemRenderEventArgs<TValue> : DropDownBaseItemRenderEventArgs<TValue>
{
    /// <summary>
    /// Gets the DropDown.
    /// </summary>
    public GroveDropDown<TValue>? DropDown { get; internal set; }
}
