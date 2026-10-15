using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Components.Editors;

/// <summary>
/// The editor of the <c>color</c> property kind (a component parameter declared
/// <c>Editor = "color"</c>): the colour picker, value in and value out, for a property pane.
/// Wraps <c>Inputs.GroveColorPicker</c>, whose own parameters are declared in markup.
/// </summary>
[BlazorComponent("ColorEditor", DisplayName = "Colour", Category = "Editors", Icon = "palette", Edits = ["color"])]
public sealed class ColorEditor : ComponentBase
{
    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Parameter]
    [ComponentOutput(DisplayName = "Changed")]
    public EventCallback<string?> Changed { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<Inputs.GroveColorPicker>(0);
        builder.AddComponentParameter(1, nameof(Inputs.GroveColorPicker.Value), Value);
        builder.AddComponentParameter(2, nameof(Inputs.GroveColorPicker.Disabled), Disabled);
        builder.AddComponentParameter(3, nameof(Inputs.GroveColorPicker.ValueChanged), EventCallback.Factory.Create<string?>(this, ChangeAsync));
        builder.CloseComponent();
    }

    private async Task ChangeAsync(string? value)
    {
        Value = value;
        await Changed.InvokeAsync(value);
    }
}
