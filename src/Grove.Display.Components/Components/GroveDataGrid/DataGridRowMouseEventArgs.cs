namespace Grove.Display.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Grove.Display.Components.Primitives.GroveDataGrid{TItem}.RowClick" /> or <see cref="Grove.Display.Components.Primitives.GroveDataGrid{TItem}.RowDoubleClick" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridRowMouseEventArgs<T> : Microsoft.AspNetCore.Components.Web.MouseEventArgs
{
    /// <summary>
    /// Gets the data item which the clicked DataGrid row represents.
    /// </summary>
    public T? Data { get; internal set; }
}

