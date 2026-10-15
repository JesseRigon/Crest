using Grove.Display.Components.Primitives;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="GroveDataGrid{TItem}.Group" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridColumnGroupEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the grouped GroveDataGridColumn.
    /// </summary>
    public GroveDataGridColumn<T>? Column { get; internal set; }

    /// <summary>
    /// Gets the group descriptor.
    /// </summary>
    public GroupDescriptor? GroupDescriptor { get; internal set; }
}

