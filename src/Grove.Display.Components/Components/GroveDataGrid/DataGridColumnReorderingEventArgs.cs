using Grove.Display.Components.Primitives;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="GroveDataGrid{TItem}.ColumnReordering" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridColumnReorderingEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the reordered GroveDataGridColumn.
    /// </summary>
    public GroveDataGridColumn<T>? Column { get; internal set; }

    /// <summary>
    /// Gets the reordered to GroveDataGridColumn.
    /// </summary>
    public GroveDataGridColumn<T>? ToColumn { get; internal set; }

    /// <summary>
    /// Gets or sets a value which will cancel the event.
    /// </summary>
    /// <value><c>true</c> to cancel the event; otherwise, <c>false</c>.</value>
    public bool Cancel { get; set; }
}

