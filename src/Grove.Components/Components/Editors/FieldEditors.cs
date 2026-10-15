using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Components.Options;
using Grove.Components.Primitives;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Components.Editors;

/// <summary>
/// An editor component (docs/blazor-display.md § 7, decision 2: editor kinds are editor components
/// in the registry): it edits one field (<see cref="FieldName"/>) or one part's own values (no
/// field) of the item in the cascaded <see cref="ContentEditorContext"/>, and declares the value
/// types it edits with <c>Edits</c>. Hidden by its visibility condition, labelled from the type
/// definition, its error under it.
/// </summary>
public abstract class FieldEditorBase : ComponentBase, IDisposable
{
    [Parameter, EditorRequired]
    [ComponentParameter(Hidden = true, Required = true)]
    public string PartName { get; set; } = string.Empty;

    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string? FieldName { get; set; }

    /// <summary>Overrides the label from the type definition.</summary>
    [Parameter]
    public string? Label { get; set; }

    [CascadingParameter]
    public ContentEditorContext? Context { get; set; }

    private ContentEditorContext? _subscribed;

    protected JsonObject? Value => Context?.Get(PartName, FieldName);

    protected bool Disabled => Context?.Disabled ?? true;

    protected Models.GroveContentFieldMetadata? Field => FieldName is null ? null : Context?.Field(PartName, FieldName);

    protected string Id => "edit-" + ContentEditorContext.Key(PartName, FieldName).Replace('.', '-');

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, Context))
        {
            if (_subscribed is not null)
            {
                _subscribed.Changed -= OnChanged;
            }

            _subscribed = Context;
            if (_subscribed is not null)
            {
                _subscribed.Changed += OnChanged;
            }
        }
    }

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnChanged;
        }
    }

    protected void Set(string property, JsonNode? value) => Context?.Set(PartName, FieldName, property, value);

    protected string? Text(string property) => Value?[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : Value?[property]?.ToString();

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Context is null || (Field is { } field && !Context.IsFieldVisible(PartName, field)))
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-editor");
        builder.AddAttribute(2, "data-testid", Id);
        builder.OpenComponent<GroveFormField>(3);
        builder.AddComponentParameter(4, nameof(GroveFormField.Text), Label ?? Field?.Label ?? Context.Part(PartName)?.DisplayName ?? FieldName ?? PartName);
        builder.AddComponentParameter(5, nameof(GroveFormField.Style), "width: 100%;");
        builder.AddComponentParameter(6, nameof(GroveFormField.ChildContent), (RenderFragment)RenderInput);
        builder.CloseComponent();
        if (Context.Errors.TryGetValue(ContentEditorContext.Key(PartName, FieldName), out var error))
        {
            builder.OpenElement(7, "div");
            builder.AddAttribute(8, "class", "grove-editor__error rz-color-danger");
            builder.AddContent(9, error);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    protected abstract void RenderInput(RenderTreeBuilder builder);

    /// <summary>A single-line text input bound to <paramref name="property"/> of the stored object.</summary>
    protected void TextInput(RenderTreeBuilder builder, int sequence, string property, string? placeholder = null)
    {
        builder.OpenComponent<GroveTextBox>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(GroveTextBox.Value), Text(property));
        builder.AddComponentParameter(sequence + 2, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, value => Set(property, JsonValue.Create(value))));
        builder.AddComponentParameter(sequence + 3, nameof(GroveTextBox.Disabled), Disabled);
        builder.AddComponentParameter(sequence + 4, nameof(GroveTextBox.Placeholder), placeholder);
        builder.AddComponentParameter(sequence + 5, nameof(GroveTextBox.Style), "width: 100%;");
        builder.CloseComponent();
    }

    /// <summary>A multi-line text input bound to <paramref name="property"/> (markup, markdown).</summary>
    protected void TextAreaInput(RenderTreeBuilder builder, int sequence, string property, int rows)
    {
        builder.OpenComponent<GroveTextArea>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(GroveTextArea.Value), Text(property));
        builder.AddComponentParameter(sequence + 2, nameof(GroveTextArea.Change), EventCallback.Factory.Create<string>(this, value => Set(property, JsonValue.Create(value))));
        builder.AddComponentParameter(sequence + 3, nameof(GroveTextArea.Disabled), Disabled);
        builder.AddComponentParameter(sequence + 4, nameof(GroveTextArea.Rows), rows);
        builder.AddComponentParameter(sequence + 5, nameof(GroveTextArea.Style), "width: 100%;");
        builder.CloseComponent();
    }
}

[BlazorComponent("TextFieldEditor", DisplayName = "Text", Category = "Editors", Icon = "short_text", Edits = ["TextField"])]
public sealed class TextFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder) => TextInput(builder, 0, "Text");
}

[BlazorComponent("TitlePartEditor", DisplayName = "Title", Category = "Editors", Icon = "title", Edits = ["TitlePart"])]
public sealed class TitlePartEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder) => TextInput(builder, 0, "Title");
}

[BlazorComponent("AutoroutePartEditor", DisplayName = "Permalink", Category = "Editors", Icon = "link", Edits = ["AutoroutePart"])]
public sealed class AutoroutePartEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder) => TextInput(builder, 0, "Path", "about/team");
}

[BlazorComponent("HtmlEditor", DisplayName = "HTML", Category = "Editors", Icon = "code", Edits = ["HtmlField", "HtmlBodyPart"])]
public sealed class HtmlEditor : MediaInsertingEditor
{
    protected override string Property => "Html";

    protected override string Snippet(Files.GroveFileItem file) => MediaSnippets.Html(file);
}

[BlazorComponent("MarkdownEditor", DisplayName = "Markdown", Category = "Editors", Icon = "notes", Edits = ["MarkdownField", "MarkdownBodyPart"])]
public sealed class MarkdownEditor : MediaInsertingEditor
{
    protected override string Property => "Markdown";

    protected override string Snippet(Files.GroveFileItem file) => MediaSnippets.Markdown(file);
}

/// <summary>
/// A body editor that inserts media by drive placement (docs/media.md, F1): the file chosen in
/// <see cref="Files.GroveDrivePicker"/> from any drive goes in as its <c>files/{placementId}</c>
/// URL, never a library path, appended at the end of the text.
/// </summary>
public abstract class MediaInsertingEditor : FieldEditorBase
{
    private bool _picking;

    protected abstract string Property { get; }

    protected abstract string Snippet(Files.GroveFileItem file);

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        TextAreaInput(builder, 0, Property, 10);
        builder.OpenComponent<GroveButton>(10);
        builder.AddComponentParameter(11, nameof(GroveButton.Text), "Insert media");
        builder.AddComponentParameter(12, nameof(GroveButton.Icon), "add_photo_alternate");
        builder.AddComponentParameter(13, nameof(GroveButton.Size), ButtonSize.Small);
        builder.AddComponentParameter(14, nameof(GroveButton.ButtonStyle), ButtonStyle.Light);
        builder.AddComponentParameter(15, nameof(GroveButton.Disabled), Disabled);
        builder.AddComponentParameter(16, nameof(GroveButton.Click), EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => _picking = true));
        builder.CloseComponent();
        builder.OpenComponent<Files.GroveDrivePicker>(17);
        builder.AddComponentParameter(18, nameof(Files.GroveDrivePicker.Open), _picking);
        builder.AddComponentParameter(19, nameof(Files.GroveDrivePicker.OpenChanged), EventCallback.Factory.Create<bool>(this, open => _picking = open));
        builder.AddComponentParameter(20, nameof(Files.GroveDrivePicker.Picked), EventCallback.Factory.Create<Files.GroveFileItem>(this, Insert));
        builder.CloseComponent();
    }

    private void Insert(Files.GroveFileItem file)
    {
        var text = Text(Property) ?? string.Empty;
        var separator = text.Length == 0 || text.EndsWith('\n') ? string.Empty : "\n";
        Set(Property, JsonValue.Create(text + separator + Snippet(file)));
    }
}

/// <summary>What an inserted file becomes in a body: an image for an image, a link otherwise, by its placement URL.</summary>
public static class MediaSnippets
{
    public static string Url(Files.GroveFileItem file) => "files/" + Uri.EscapeDataString(file.Id);

    public static bool IsImage(Files.GroveFileItem file) =>
        file.Type?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
        || file.Extension is "png" or "jpg" or "jpeg" or "gif" or "webp" or "svg" or "avif";

    public static string Html(Files.GroveFileItem file)
    {
        var url = System.Net.WebUtility.HtmlEncode(Url(file));
        var text = System.Net.WebUtility.HtmlEncode(file.Metadata.AlternativeText ?? file.Metadata.Title ?? file.Name);
        return IsImage(file) ? $"<img src=\"{url}\" alt=\"{text}\" />" : $"<a href=\"{url}\">{text}</a>";
    }

    public static string Markdown(Files.GroveFileItem file)
    {
        var text = (file.Metadata.AlternativeText ?? file.Metadata.Title ?? file.Name).Replace("]", "\\]", StringComparison.Ordinal).Replace("[", "\\[", StringComparison.Ordinal);
        return IsImage(file) ? $"![{text}]({Url(file)})" : $"[{text}]({Url(file)})";
    }
}

[BlazorComponent("LinkFieldEditor", DisplayName = "Link", Category = "Editors", Icon = "link", Edits = ["LinkField"])]
public sealed class LinkFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-editor__pair");
        TextInput(builder, 10, "Url", "https://");
        TextInput(builder, 20, "Text");
        builder.CloseElement();
    }
}

[BlazorComponent("NumericFieldEditor", DisplayName = "Number", Category = "Editors", Icon = "pin", Edits = ["NumericField"])]
public sealed class NumericFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var node = Value?["Value"];
        decimal? value = node is null ? null : Convert.ToDecimal(node.GetValue<object>(), CultureInfo.InvariantCulture);
        builder.OpenComponent<GroveNumeric<decimal?>>(0);
        builder.AddComponentParameter(1, "Value", value);
        builder.AddComponentParameter(2, "ValueChanged", EventCallback.Factory.Create<decimal?>(this, changed => Set("Value", changed is null ? null : JsonValue.Create(changed))));
        builder.AddComponentParameter(3, "Disabled", Disabled);
        builder.AddComponentParameter(4, "Style", "width: 100%;");
        builder.CloseComponent();
    }
}

[BlazorComponent("BooleanFieldEditor", DisplayName = "Yes/no", Category = "Editors", Icon = "check_box", Edits = ["BooleanField"])]
public sealed class BooleanFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenComponent<GroveCheckBox<bool>>(0);
        builder.AddComponentParameter(1, "Value", Value?["Value"]?.GetValue<bool>() == true);
        builder.AddComponentParameter(2, "ValueChanged", EventCallback.Factory.Create<bool>(this, changed => Set("Value", JsonValue.Create(changed))));
        builder.AddComponentParameter(3, "Disabled", Disabled);
        builder.CloseComponent();
    }
}

[BlazorComponent("DateFieldEditor", DisplayName = "Date", Category = "Editors", Icon = "event", Edits = ["DateField", "DateTimeField"])]
public sealed class DateFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenComponent<GroveDatePicker<DateTime?>>(0);
        builder.AddComponentParameter(1, "Value", ContentEditorContext.ReadDate(Value?["Value"]));
        builder.AddComponentParameter(2, "ValueChanged", EventCallback.Factory.Create<DateTime?>(this, changed => Set("Value", changed is null ? null : JsonValue.Create(changed))));
        builder.AddComponentParameter(3, "Disabled", Disabled);
        builder.AddComponentParameter(4, "Style", "width: 100%;");
        builder.CloseComponent();
    }
}

[BlazorComponent("ContentPickerFieldEditor", DisplayName = "Content items", Category = "Editors", Icon = "link", Edits = ["ContentPickerField"])]
public sealed class ContentPickerFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var ids = Value?["ContentItemIds"] is JsonArray array
            ? string.Join(", ", array.Select(id => id?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)))
            : string.Empty;

        builder.OpenComponent<GroveTextBox>(0);
        builder.AddComponentParameter(1, nameof(GroveTextBox.Value), ids);
        builder.AddComponentParameter(2, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, value => Set("ContentItemIds", new JsonArray(value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => (JsonNode?)JsonValue.Create(id))
            .ToArray()))));
        builder.AddComponentParameter(3, nameof(GroveTextBox.Disabled), Disabled);
        builder.AddComponentParameter(4, nameof(GroveTextBox.Style), "width: 100%;");
        builder.CloseComponent();
    }
}

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
        IReadOnlyList<string> selected = Value?["SelectedIds"] is JsonArray ids
            ? [.. ids.Select(id => id?.GetValue<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!)]
            : [];

        builder.OpenComponent<GroveOptionPicker>(0);
        builder.AddComponentParameter(1, "SourceKey", settings?["SourceKey"]?.GetValue<string>() ?? string.Empty);
        builder.AddComponentParameter(2, "Columns", ContentEditorContext.PickerColumns(settings));
        builder.AddComponentParameter(3, "SelectedIds", selected);
        builder.AddComponentParameter(4, "SelectedIdsChanged", EventCallback.Factory.Create<IReadOnlyList<string>>(this, changed =>
            Set("SelectedIds", new JsonArray([.. changed.Select(id => (JsonNode?)JsonValue.Create(id))]))));
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
        builder.CloseComponent();
    }
}

/// <summary>The editor of last resort (<c>*</c>): the stored object as JSON, replaced whole when it parses.</summary>
[BlazorComponent("JsonEditor", DisplayName = "JSON", Category = "Editors", Icon = "data_object", Edits = ["*"])]
public sealed class JsonEditor : FieldEditorBase
{
    private string? _error;

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenComponent<GroveTextArea>(0);
        builder.AddComponentParameter(1, nameof(GroveTextArea.Value), Value?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}");
        builder.AddComponentParameter(2, nameof(GroveTextArea.Change), EventCallback.Factory.Create<string>(this, Replace));
        builder.AddComponentParameter(3, nameof(GroveTextArea.Disabled), Disabled);
        builder.AddComponentParameter(4, nameof(GroveTextArea.Rows), 6);
        builder.AddComponentParameter(5, nameof(GroveTextArea.Style), "width: 100%; font-family: var(--grove-font-mono, monospace);");
        builder.CloseComponent();
        if (_error is not null)
        {
            builder.OpenElement(6, "div");
            builder.AddAttribute(7, "class", "grove-editor__error rz-color-danger");
            builder.AddContent(8, _error);
            builder.CloseElement();
        }
    }

    private void Replace(string text)
    {
        try
        {
            if (JsonNode.Parse(text) is JsonObject replacement && Context is { } context)
            {
                var target = context.GetOrCreate(PartName, FieldName);
                foreach (var name in target.Select(pair => pair.Key).ToArray())
                {
                    target.Remove(name);
                }

                foreach (var (name, value) in replacement.ToArray())
                {
                    replacement.Remove(name);
                    target[name] = value;
                }

                _error = null;
                context.Touch(PartName, FieldName);
            }
            else
            {
                _error = "A JSON object is expected.";
            }
        }
        catch (JsonException exception)
        {
            _error = exception.Message;
        }
    }
}
