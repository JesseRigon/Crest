using System;
using Microsoft.AspNetCore.Components;

using Grove.Components.Primitives.Spreadsheet;
using Grove.Components.Primitives.Documents.Spreadsheet;
namespace Grove.Components.Primitives;

#nullable enable

/// <summary>
/// Base class for spreadsheet toolbar buttons that need to react to selection changes.
/// </summary>
public abstract class GroveSpreadsheetToolBase : ComponentBase, IDisposable
{
    /// <summary>
    /// The active worksheet, cascaded from the host <see cref="Grove.Components.Primitives.GroveSpreadsheet"/>.
    /// </summary>
    [CascadingParameter]
    public Worksheet? Worksheet { get; set; }

    /// <summary>
    /// The host spreadsheet, cascaded from <see cref="Grove.Components.Primitives.GroveSpreadsheet"/>.
    /// </summary>
    [CascadingParameter]
    public ISpreadsheet? Spreadsheet { get; set; }

    /// <summary>
    /// Gets the feature this tool drives, or <c>null</c> for tools that never need to
    /// grey out beyond the default selection check.
    /// </summary>
    protected virtual SpreadsheetFeature? Feature => null;

    /// <summary>
    /// Gets whether the tool should be disabled.
    /// </summary>
    protected virtual bool IsDisabled
        => Worksheet is null
        || Worksheet.Selection.Cell == CellRef.Invalid
        || (Feature is SpreadsheetFeature f && Spreadsheet is not null && !Spreadsheet.IsFeatureAllowed(f));

    private readonly EventBinding<Selection> selectionBinding;

    /// <summary>
    /// Initializes a new instance of the <see cref="GroveSpreadsheetToolBase"/> class.
    /// </summary>
    protected GroveSpreadsheetToolBase()
    {
        selectionBinding = new EventBinding<Selection>(
            s => s.Changed += OnSelectionChanged,
            s => s.Changed -= OnSelectionChanged);
    }

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        selectionBinding.Bind(Worksheet?.Selection);
    }

    private void OnSelectionChanged()
    {
        StateHasChanged();
    }

    void IDisposable.Dispose()
    {
        selectionBinding.Dispose();
    }
}
