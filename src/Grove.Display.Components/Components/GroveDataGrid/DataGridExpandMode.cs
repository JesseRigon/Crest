namespace Grove.Display.Components.Primitives;

/// <summary>
/// Specifies the expand behavior of <see cref="Grove.Display.Components.Primitives.GroveDataGrid{TItem}" />.
/// </summary>
public enum DataGridExpandMode
{
    /// <summary>
    /// The user can expand only one row at a time. Expanding a different row collapses the last expanded row.
    /// </summary>
    Single,

    /// <summary>
    /// The user can expand multiple rows.
    /// </summary>
    Multiple
}

