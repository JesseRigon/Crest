using Grove.Display.Components.Editors;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Text.Json.Nodes;

namespace Grove.Display.Icons.Components;

/// <summary>
/// A text field with the <c>IconPicker</c> editor (docs/MVC/parity.md › New components): an icon
/// picked from the tenant's icon sets with search (<see cref="IconSelector"/>), stored as the
/// icon's key in the field's text. The editor tree places it for a text field whose editor
/// setting is <c>IconPicker</c> (an editor declared for <c>{FieldType}-{Editor}</c>).
/// </summary>
[BlazorComponent("IconPicker", DisplayName = "Icon", Category = "Editors", Icon = "interests", Edits = ["TextField-IconPicker"])]
public sealed class IconPickerEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenComponent<IconSelector>(0);
        builder.AddComponentParameter(1, nameof(IconSelector.Value), Text("Text"));
        builder.AddComponentParameter(2, nameof(IconSelector.ValueChanged), EventCallback.Factory.Create<string?>(this, value => Set("Text", JsonValue.Create(value))));
        builder.CloseComponent();
    }
}
