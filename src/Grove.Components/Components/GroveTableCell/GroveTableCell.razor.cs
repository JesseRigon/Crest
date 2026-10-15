namespace Grove.Components.Primitives;

/// <summary>
/// Represents a cell in <see cref="GroveTable"/>
/// </summary>
public partial class GroveTableCell : GroveComponentWithChildren
{
    /// <inheritdoc />
    protected override string GetComponentCssClass() => "rz-data-cell";
}