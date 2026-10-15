using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;

namespace Grove.Components.Primitives;

/// <summary>
/// GroveCounter component - a minimal interactive island. Needs an actual interactive
/// render mode (InteractiveServer/InteractiveWebAssembly/InteractiveAuto) to be
/// clickable - rendered through BlazorComponentShapeBindingResolver's
/// Static-SSR-only HtmlRenderer path, the button renders but is inert (no circuit, no
/// WASM runtime attached).
/// </summary>
[BlazorComponent("GroveCounter", DisplayName = "Counter", Category = "Diagnostics", Icon = "add_circle")]
public partial class GroveCounter : ComponentBase
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
