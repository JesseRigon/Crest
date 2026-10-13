using System.Text.Json.Nodes;
using Crest.Components.Editors;
using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Crest.Components.Files;

/// <summary>
/// The media field's editor: files chosen with the drive-aware picker from any drive the
/// editor can see, stored as placement ids (docs/media.md, ruling F1). Replaces editing
/// media library paths by hand.
/// </summary>
[BlazorComponent("MediaFieldEditor", DisplayName = "Media", Category = "Editors", Icon = "perm_media", Edits = ["MediaField"])]
public sealed class MediaFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CrestMediaFieldInput>(0);
        builder.AddComponentParameter(1, nameof(CrestMediaFieldInput.Value), Value);
        builder.AddComponentParameter(2, nameof(CrestMediaFieldInput.Disabled), Disabled);
        builder.AddComponentParameter(3, nameof(CrestMediaFieldInput.Changed), EventCallback.Factory.Create<(string Property, JsonArray Value)>(this, change => Set(change.Property, change.Value)));
        builder.CloseComponent();
    }
}
