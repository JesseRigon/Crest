using System;
using Microsoft.AspNetCore.Components;
using Grove.Display.Components.Primitives.Rendering;

namespace Grove.Display.Components.Primitives;

/// <summary>
/// Display a styled table with data.
/// </summary>
public partial class GroveTable : GroveComponentWithChildren
{
    /// <summary>
    /// Gets or sets the grid lines style.
    /// </summary>
    /// <value>The grid lines.</value>
    [Parameter]
    public DataGridGridLines GridLines { get; set; } = DataGridGridLines.Default;

    /// <summary>
    /// Gets or sets a value indicating whether GroveTable should use alternating row styles.
    /// </summary>
    /// <value><c>true</c> if GroveTable is using alternating row styles; otherwise, <c>false</c>.</value>
    [Parameter]
    public bool AllowAlternatingRows { get; set; } = true;

    /// <inheritdoc />
    protected override string GetComponentCssClass() => ClassList.Create("rz-data-grid rz-datatable rz-datatable-scrollable")
        .Add("rz-has-height", CurrentStyle.ContainsKey("height"))
        .ToString();

    /// <summary>
    /// Gets the table CSS classes.
    /// </summary>
    protected virtual string GetTableCssClass()
    {
        var enumName = Enum.GetName<DataGridGridLines>(GridLines);
        return ClassList.Create("rz-grid-table rz-grid-table-fixed")
            .Add("rz-grid-table-striped", AllowAlternatingRows)
            .Add($"rz-grid-gridlines-{(enumName ?? GridLines.ToString()).ToLowerInvariant()}", GridLines != DataGridGridLines.Default)
            .ToString();
    }
}