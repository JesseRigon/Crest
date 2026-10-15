using Grove.Display.Components.Primitives;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="GroveDataGrid{TItem}.Sort" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridColumnSortEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the sorted GroveDataGridColumn.
    /// </summary>
    public GroveDataGridColumn<T>? Column { get; internal set; }

    /// <summary>
    /// Gets the new sort order of the sorted column.
    /// </summary>
    public SortOrder? SortOrder { get; internal set; }
}

