using Grove.Components.Primitives;

namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about GroveListBox ItemRender event.
/// </summary>
public class ListBoxItemRenderEventArgs<TValue> : DropDownBaseItemRenderEventArgs<TValue>
{
    /// <summary>
    /// Gets the ListBox.
    /// </summary>
    public GroveListBox<TValue>? ListBox { get; internal set; }
}

