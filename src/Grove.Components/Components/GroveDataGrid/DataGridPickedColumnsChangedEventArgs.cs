using System.Collections.Generic;
using Grove.Components.Primitives;

namespace Grove.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="GroveDataGrid{TItem}.PickedColumnsChanged" /> event that is being raised.
/// </summary>
/// <typeparam name="T">The data item type.</typeparam>
public class DataGridPickedColumnsChangedEventArgs<T> where T : notnull
{
    /// <summary>
    /// Gets the picked columns.
    /// </summary>
    public IEnumerable<GroveDataGridColumn<T>>? Columns { get; internal set; }
}

