using System.Text.Json.Nodes;
using Grove.Components.Editors;
using Grove.DisplayManagement.Components;
using Grove.OptionPicker.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.OptionPicker.Editors;

/// <summary>
/// The OptionPickerField editor (the editor facet, docs/blazor-display.md § 1): the option picker
/// over the field's source, with the attachment's columns, filters and cascade. A content item
/// source also lists the selection in order with edit links and offers a create link per allowed
/// type the user may create.
/// </summary>
[BlazorComponent("OptionPickerFieldEditor", DisplayName = "Options", Category = "Editors", Icon = "checklist", Edits = ["OptionPickerField"])]
public sealed class OptionPickerFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        if (Field is not { } field || Context is not { } context)
        {
            return;
        }

        var settings = ContentEditorContext.PickerSettings(field);
        var sourceKey = settings?["SourceKey"]?.GetValue<string>() ?? string.Empty;
        IReadOnlyList<string> selected = Value?["SelectedIds"] is JsonArray ids
            ? [.. ids.Select(id => id?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!)]
            : [];
        var isContentItems = sourceKey.StartsWith("contentitem:", StringComparison.OrdinalIgnoreCase);

        builder.OpenComponent<GroveOptionPicker>(0);
        builder.AddComponentParameter(1, "SourceKey", sourceKey);
        builder.AddComponentParameter(2, "Columns", ContentEditorContext.PickerColumns(settings));
        builder.AddComponentParameter(3, "SelectedIds", selected);
        builder.AddComponentParameter(4, "SelectedIdsChanged", EventCallback.Factory.Create<IReadOnlyList<string>>(this, changed =>
        {
            Set("SelectedIds", new JsonArray([.. changed.Select(id => (JsonNode?)JsonValue.Create(id))]));
            Set("SourceKey", JsonValue.Create(sourceKey));
        }));
        builder.AddComponentParameter(5, "Multiple", settings?["Multiple"]?.GetValue<bool>() ?? false);
        builder.AddComponentParameter(6, "Required", field.Required);
        builder.AddComponentParameter(7, "Placeholder", settings?["Placeholder"]?.GetValue<string>() ?? string.Empty);
        builder.AddComponentParameter(8, "Disabled", Disabled);
        // The attachment identifies itself; the server loads its filters and resolves any
        // ValueFrom against the editor state.
        builder.AddComponentParameter(9, "ContentType", context.Metadata?.ContentType);
        builder.AddComponentParameter(10, "FieldName", field.Name);
        builder.AddComponentParameter(11, "EditorState", context.EditorState(field));
        builder.AddComponentParameter(12, "TestId", $"picker-{PartName}-{field.Name}");
        builder.AddComponentParameter(13, "ShowSelectedList", isContentItems);
        builder.AddComponentParameter(14, "ShowCreateLinks", isContentItems);
        builder.CloseComponent();
    }
}
