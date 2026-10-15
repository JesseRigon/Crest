namespace Grove.Components.Primitives;

/// <summary>
/// Specifies the selection mode behavior of <see cref="Grove.Components.Primitives.GroveDataGrid{TItem}" />.
/// </summary>
public enum DataGridSelectionMode
{
    /// <summary>
    /// The user can select only one row at a time. Selecting a different row deselects the last selected row.
    /// </summary>
    Single,

    /// <summary>
    /// The user can select multiple rows.
    /// </summary>
    Multiple
}

