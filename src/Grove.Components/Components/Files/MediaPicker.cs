using Grove.Components.Primitives;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Components.Files;

/// <summary>
/// The editor of the <c>media</c> property kind (<see cref="ComponentSchema.Editors.Media"/>, a
/// component parameter such as <c>Figure.Path</c>): a file chosen from any drive the editor can
/// see through <see cref="GroveDrivePicker"/>, stored as its placement id (docs/media.md, ruling
/// F1: ids are stored); <see cref="MediaReference.Url"/> turns it into the file's <c>/files</c>
/// URL when the component renders. A value-in, value-out editor, for a property pane.
/// </summary>
[BlazorComponent("MediaPicker", DisplayName = "Media", Category = "Editors", Icon = "perm_media", Edits = [ComponentSchema.Editors.Media])]
public sealed class MediaPicker : ComponentBase
{
    private bool _open;
    private string? _name;

    /// <summary>The placement id (or, for a value set before ids, a URL).</summary>
    [Parameter]
    [ComponentParameter(Editor = ComponentSchema.Editors.Media)]
    public string? Value { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>The placement id of the file chosen; null when cleared.</summary>
    [Parameter]
    [ComponentOutput(DisplayName = "Changed")]
    public EventCallback<string?> Changed { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-media-picker");
        builder.OpenElement(2, "span");
        builder.AddAttribute(3, "class", "grove-media-picker__value");
        builder.AddContent(4, string.IsNullOrEmpty(Value) ? "No file chosen." : _name ?? Value);
        builder.CloseElement();

        builder.OpenComponent<GroveButton>(5);
        builder.AddComponentParameter(6, nameof(GroveButton.Text), "Choose a file");
        builder.AddComponentParameter(7, nameof(GroveButton.Icon), "attach_file");
        builder.AddComponentParameter(8, nameof(GroveButton.Size), ButtonSize.Small);
        builder.AddComponentParameter(9, nameof(GroveButton.Disabled), Disabled);
        builder.AddComponentParameter(10, nameof(GroveButton.Click), EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => _open = true));
        builder.CloseComponent();

        if (!string.IsNullOrEmpty(Value))
        {
            builder.OpenComponent<GroveButton>(11);
            builder.AddComponentParameter(12, nameof(GroveButton.Icon), "close");
            builder.AddComponentParameter(13, nameof(GroveButton.Variant), Variant.Text);
            builder.AddComponentParameter(14, nameof(GroveButton.Size), ButtonSize.Small);
            builder.AddComponentParameter(15, nameof(GroveButton.Disabled), Disabled);
            builder.AddComponentParameter(16, nameof(GroveButton.Click), EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => ChooseAsync(null, null)));
            builder.CloseComponent();
        }

        builder.OpenComponent<GroveDrivePicker>(17);
        builder.AddComponentParameter(18, nameof(GroveDrivePicker.Open), _open);
        builder.AddComponentParameter(19, nameof(GroveDrivePicker.OpenChanged), EventCallback.Factory.Create<bool>(this, open => _open = open));
        builder.AddComponentParameter(20, nameof(GroveDrivePicker.Picked), EventCallback.Factory.Create<GroveFileItem>(this, item => ChooseAsync(item.Id, item.Name)));
        builder.CloseComponent();
        builder.CloseElement();
    }

    private async Task ChooseAsync(string? id, string? name)
    {
        Value = id;
        _name = name;
        await Changed.InvokeAsync(id);
    }
}

/// <summary>
/// A <c>media</c> property's value as the URL a component renders: a placement id is the file's
/// <c>files/{id}</c> URL (relative to the shell's base, served with the access check, ruling F1);
/// a URL or path set before ids stays as it is, if it is safe.
/// </summary>
public static class MediaReference
{
    public static string? Url(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (IsPlacementId(trimmed))
        {
            return "files/" + Uri.EscapeDataString(trimmed);
        }

        return Content.SafeUrl.IsSafe(trimmed) ? trimmed : null;
    }

    /// <summary>An id has no path, scheme or extension: letters, digits, '-' and '_'.</summary>
    public static bool IsPlacementId(string value) =>
        value.Length is > 0 and <= 64 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
