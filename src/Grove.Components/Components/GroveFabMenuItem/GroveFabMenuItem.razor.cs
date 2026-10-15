using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives;

/// <summary>
/// GroveFabMenuItem component.
/// </summary>
/// <example>
/// <code>
/// &lt;GroveFabMenuItem Text="Folder" Icon="folder" Click=@(args => Console.WriteLine("Item clicked")) /&gt;
/// </code>
/// </example>
public partial class GroveFabMenuItem : GroveButton
{
    /// <inheritdoc />
    [Parameter]
    public override Variant Variant { get; set; } = Variant.Flat;

    /// <inheritdoc />
    [Parameter]
    public override Shade Shade { get; set; } = Shade.Light;

    /// <inheritdoc />
    [Parameter]
    public override ButtonSize Size { get; set; } = ButtonSize.Large;
}