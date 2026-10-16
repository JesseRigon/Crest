using System.Text.Json.Nodes;
using Grove.Contents.OptionPicker.Components;
using Grove.Display.Components.Editors;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Contents.OptionPicker.Editors;

/// <summary>
/// The owner of an item, on the <c>Common</c> field set when its settings show the owner editor:
/// one user picked from the users option source, kept as the item's <c>Owner</c> (the user's id)
/// and <c>OwnerName</c>.
/// </summary>
[BlazorComponent("OwnerEditor", DisplayName = "Owner", Category = "Editors", Icon = "person")]
public sealed class OwnerEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var owner = Value?["Owner"]?.ToString();
        builder.OpenComponent<OptionListPicker>(0);
        builder.AddComponentParameter(1, "SourceKey", Grove.Models.OptionSourceKeys.UserProvider);
        builder.AddComponentParameter(2, "SelectedIds", string.IsNullOrEmpty(owner) ? Array.Empty<string>() : new[] { owner });
        builder.AddComponentParameter(3, "SelectedIdsChanged", EventCallback.Factory.Create<IReadOnlyList<string>>(this, changed =>
        {
            Set("Owner", JsonValue.Create(changed.FirstOrDefault()));
        }));
        builder.AddComponentParameter(4, "Required", true);
        builder.AddComponentParameter(5, "Disabled", Disabled);
        builder.AddComponentParameter(6, "Placeholder", "Search users");
        builder.AddComponentParameter(7, "TestId", "owner-editor");
        builder.CloseComponent();
    }
}
