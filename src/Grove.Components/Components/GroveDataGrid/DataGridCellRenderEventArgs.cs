using Grove.Components.Primitives;

namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="GroveDataGrid{TItem}.CellRender" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridCellRenderEventArgs<T> : RowRenderEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the GroveDataGridColumn which this cells represents.
    /// </summary>
    public GroveDataGridColumn<T>? Column { get; internal set; }
}

