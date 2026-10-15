using Grove.Components.Primitives;

namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="GroveDataGrid{TItem}.CellContextMenu" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridCellMouseEventArgs<T> : Microsoft.AspNetCore.Components.Web.MouseEventArgs where T : notnull
{
    /// <summary>
    /// Gets the data item which the clicked DataGrid row represents.
    /// </summary>
    public T? Data { get; internal set; }

    /// <summary>
    /// Gets the GroveDataGridColumn which this cells represents.
    /// </summary>
    public GroveDataGridColumn<T>? Column { get; internal set; }
}

