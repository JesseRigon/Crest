using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives;

/// <summary>
/// CrestCounter component - a minimal interactive island. Needs an actual interactive
/// render mode (InteractiveServer/InteractiveWebAssembly/InteractiveAuto) to be
/// clickable - rendered through CrestBlazorComponentShapeBindingResolver's
/// Static-SSR-only HtmlRenderer path, the button renders but is inert (no circuit, no
/// WASM runtime attached).
/// </summary>
[CrestBlazorComponent("CrestCounter", DisplayName = "Counter", Category = "Diagnostics", Icon = "add_circle")]
public partial class CrestCounter : ComponentBase
{
    [Parameter]
    [ComponentParameter(DisplayName = "Start value")]
    public int StartValue { get; set; }

    private int Count { get; set; }

    protected override void OnParametersSet()
    {
        Count = StartValue;
    }

    private void Increment()
    {
        Count++;
    }
}
