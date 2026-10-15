using Grove.Display.Components.Primitives;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="GroveDataGrid{TItem}.ColumnReordered" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridColumnReorderedEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the reordered GroveDataGridColumn.
    /// </summary>
    public GroveDataGridColumn<T>? Column { get; internal set; }

    /// <summary>
    /// Gets the old index of the column.
    /// </summary>
    public int OldIndex { get; internal set; }

    /// <summary>
    /// Gets the new index of the column.
    /// </summary>
    public int NewIndex { get; internal set; }
}

