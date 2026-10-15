using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Components.Primitives;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

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

    /// <summary>
    /// The editor setting of the field (<c>ContentPartFieldSettings.Editor</c>) or, for a part's
    /// own values, of the part on the type (<c>ContentTypePartSettings.Editor</c>): the mode the
    /// editor renders in ("Switch", "Color", "Monaco"...); empty for the default.
    /// </summary>
    protected string Mode => Field?.Editor is { Length: > 0 } editor
        ? editor
        : Context?.Part(PartName)?.Settings?["ContentTypePartSettings"]?["Editor"] is JsonValue value && value.TryGetValue<string>(out var partEditor) ? partEditor : string.Empty;

    /// <summary>The value type being edited: the field's type, else the part's name (a part edited for its own values).</summary>
    protected string ValueType => Field?.FieldType ?? PartName;

    protected bool IsMode(string mode) => string.Equals(Mode, mode, StringComparison.OrdinalIgnoreCase);

    /// <summary>A value of the field's settings (<c>Settings[settingsType][property]</c>).</summary>
    protected JsonNode? Setting(string settingsType, string property) => Field?.Settings?[settingsType]?[property];

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
        if (Context is null || (Field is { } field && !Context.IsFieldVisible(PartName, field)) || !IsShown)
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

    /// <summary>Whether the editor shows at all (a part whose settings hide its input).</summary>
    protected virtual bool IsShown => true;

    /// <summary>A single-line text input bound to <paramref name="property"/> of the stored object.</summary>
    protected void TextInput(RenderTreeBuilder builder, int sequence, string property, string? placeholder = null, string? type = null)
    {
        builder.OpenComponent<GroveTextBox>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(GroveTextBox.Value), Text(property));
        builder.AddComponentParameter(sequence + 2, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, value => Set(property, JsonValue.Create(value))));
        builder.AddComponentParameter(sequence + 3, nameof(GroveTextBox.Disabled), Disabled);
        builder.AddComponentParameter(sequence + 4, nameof(GroveTextBox.Placeholder), placeholder);
        builder.AddComponentParameter(sequence + 5, nameof(GroveTextBox.Style), "width: 100%;");
        if (type is not null)
        {
            builder.AddAttribute(sequence + 6, "type", type);
        }

        builder.CloseComponent();
    }

    /// <summary>The code editor bound to <paramref name="property"/>.</summary>
    protected void CodeInput(RenderTreeBuilder builder, int sequence, string property, string? language)
    {
        builder.OpenComponent<CodeEditor>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(CodeEditor.Value), Text(property));
        builder.AddComponentParameter(sequence + 2, nameof(CodeEditor.Changed), EventCallback.Factory.Create<string?>(this, value => Set(property, JsonValue.Create(value))));
        builder.AddComponentParameter(sequence + 3, nameof(CodeEditor.Disabled), Disabled);
        builder.AddComponentParameter(sequence + 4, nameof(CodeEditor.Language), language);
        builder.AddComponentParameter(sequence + 5, nameof(CodeEditor.Id), Id);
        builder.CloseComponent();
    }

    /// <summary>The colour picker bound to <paramref name="property"/>.</summary>
    protected void ColorInput(RenderTreeBuilder builder, int sequence, string property)
    {
        builder.OpenComponent<ColorEditor>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(ColorEditor.Value), Text(property));
        builder.AddComponentParameter(sequence + 2, nameof(ColorEditor.Changed), EventCallback.Factory.Create<string?>(this, value => Set(property, JsonValue.Create(value))));
        builder.AddComponentParameter(sequence + 3, nameof(ColorEditor.Disabled), Disabled);
        builder.CloseComponent();
    }

    /// <summary>The language a code mode edits in: the Monaco settings' <c>language</c>, else <paramref name="fallback"/>.</summary>
    protected string CodeLanguage(string monacoSettingsType, string fallback)
    {
        if (Setting(monacoSettingsType, "Options") is JsonValue options && options.TryGetValue<string>(out var json) && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                if (JsonNode.Parse(json)?["language"] is JsonValue language && language.TryGetValue<string>(out var id) && !string.IsNullOrWhiteSpace(id))
                {
                    return id;
                }
            }
            catch (JsonException)
            {
            }
        }

        return fallback;
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

/// <summary>
/// One line of text: a text field (its editor setting picks the input: email, telephone, URL, a
/// text area, a predefined list, a colour, code), an alias, a YouTube field's address (the field
/// handler derives the embed address).
/// </summary>
[BlazorComponent("TextFieldEditor", DisplayName = "Text", Category = "Editors", Icon = "short_text", Edits = ["TextField", "AliasPart", "YoutubeField", "AuditTrailPart"])]
public sealed class TextFieldEditor : FieldEditorBase
{
    // The audit trail part's comment is saved with the version, when the type asks for it.
    protected override bool IsShown => ValueType != "AuditTrailPart"
        || !(Context?.Part(PartName)?.Settings?["AuditTrailPartSettings"]?["ShowCommentInput"] is JsonValue show && show.TryGetValue<bool>(out var shown) && !shown);

    private string Property => ValueType switch
    {
        "AliasPart" => "Alias",
        "AuditTrailPart" => "Comment",
        "YoutubeField" => "RawAddress",
        _ => "Text",
    };

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var property = Property;
        var placeholder = Setting("TextFieldSettings", "Placeholder")?.ToString();
        if (ValueType == "AuditTrailPart")
        {
            TextAreaInput(builder, 10, property, 2);
            return;
        }

        switch (Mode.ToLowerInvariant())
        {
            case "email":
                TextInput(builder, 0, property, placeholder, "email");
                break;
            case "tel":
                TextInput(builder, 0, property, placeholder, "tel");
                break;
            case "url":
                TextInput(builder, 0, property, placeholder, "url");
                break;
            case "textarea":
                TextAreaInput(builder, 10, property, 4);
                break;
            case "color":
                ColorInput(builder, 20, property);
                break;
            case "monaco":
            case "codemirror":
                CodeInput(builder, 30, property, CodeLanguage("TextFieldMonacoEditorSettings", "plaintext"));
                break;
            case "predefinedlist":
                PredefinedList(builder, property);
                break;
            default:
                TextInput(builder, 0, property, ValueType == "YoutubeField" ? "https://www.youtube.com/watch?v=" : placeholder);
                break;
        }
    }

    private void PredefinedList(RenderTreeBuilder builder, string property)
    {
        var options = Setting("TextFieldPredefinedListEditorSettings", "Options") is JsonArray array
            ? array.Select(option => new Forms.SelectOption(option?["name"]?.ToString() ?? option?["value"]?.ToString() ?? string.Empty, option?["value"]?.ToString() ?? string.Empty)).ToList()
            : [];
        var radio = Setting("TextFieldPredefinedListEditorSettings", "Editor") is JsonValue editor
            && (editor.TryGetValue<int>(out var number) ? number == 0 : string.Equals(editor.ToString(), "Radio", StringComparison.OrdinalIgnoreCase));
        var value = Text(property) ?? Setting("TextFieldPredefinedListEditorSettings", "DefaultValue")?.ToString();
        var changed = EventCallback.Factory.Create<string>(this, selected => Set(property, JsonValue.Create(selected)));
        if (radio)
        {
            builder.OpenComponent<GroveRadioButtonList<string>>(40);
            builder.AddComponentParameter(41, "Data", options);
            builder.AddComponentParameter(42, "TextProperty", nameof(Forms.SelectOption.Text));
            builder.AddComponentParameter(43, "ValueProperty", nameof(Forms.SelectOption.Value));
            builder.AddComponentParameter(44, "Value", value);
            builder.AddComponentParameter(45, "ValueChanged", changed);
            builder.AddComponentParameter(46, "Disabled", Disabled);
            builder.CloseComponent();
            return;
        }

        builder.OpenComponent<GroveDropDown<string>>(50);
        builder.AddComponentParameter(51, "Data", options);
        builder.AddComponentParameter(52, "TextProperty", nameof(Forms.SelectOption.Text));
        builder.AddComponentParameter(53, "ValueProperty", nameof(Forms.SelectOption.Value));
        builder.AddComponentParameter(54, "Value", value);
        builder.AddComponentParameter(55, "ValueChanged", changed);
        builder.AddComponentParameter(56, "Disabled", Disabled);
        builder.AddComponentParameter(57, "AllowClear", Field?.Required != true);
        builder.AddComponentParameter(58, "Style", "width: 100%;");
        builder.CloseComponent();
    }
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

    protected override string CodeLanguageId => "markdown";

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
    private IReadOnlyList<ShortcodeChoice>? _shortcodes;
    private bool _shortcodesLoaded;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    /// <summary>A shortcode the editor can insert (<c>api/grove/shortcodes</c>).</summary>
    public sealed record ShortcodeChoice(string Name, string DefaultValue, string? Hint);

    protected override async Task OnParametersSetAsync()
    {
        if (_shortcodesLoaded || !RendererInfo.IsInteractive)
        {
            return;
        }

        // The shortcode tool: only when the Shortcodes feature answers.
        _shortcodesLoaded = true;
        if (Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetService<HttpClient>(Services) is not { } http)
        {
            return;
        }

        try
        {
            using var response = await http.GetAsync("api/grove/shortcodes");
            if (response.IsSuccessStatusCode)
            {
                _shortcodes = await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<List<ShortcodeChoice>>(response.Content, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
        }
    }

    protected abstract string Property { get; }

    protected abstract string Snippet(Files.GroveFileItem file);

    /// <summary>The language of the code mode (the editor setting <c>Monaco</c>).</summary>
    protected virtual string CodeLanguageId => "html";

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        if (IsMode("Monaco"))
        {
            CodeInput(builder, 30, Property, CodeLanguage(ValueType == "HtmlBodyPart" ? "HtmlBodyPartMonacoEditorSettings" : "HtmlFieldMonacoEditorSettings", CodeLanguageId));
            return;
        }

        TextAreaInput(builder, 0, Property, IsMode("Multiline") ? 6 : 10);
        if (IsMode("Multiline"))
        {
            return;
        }

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
        if (_shortcodes is { Count: > 0 } shortcodes)
        {
            builder.OpenComponent<GroveDropDown<string>>(30);
            builder.AddComponentParameter(31, "Data", shortcodes);
            builder.AddComponentParameter(32, "TextProperty", nameof(ShortcodeChoice.Name));
            builder.AddComponentParameter(33, "ValueProperty", nameof(ShortcodeChoice.DefaultValue));
            builder.AddComponentParameter(34, "Value", (string?)null);
            builder.AddComponentParameter(35, "ValueChanged", EventCallback.Factory.Create<string>(this, InsertText));
            builder.AddComponentParameter(36, "Placeholder", "Insert shortcode");
            builder.AddComponentParameter(37, "AllowFiltering", true);
            builder.AddComponentParameter(38, "Disabled", Disabled);
            builder.AddComponentParameter(39, "Style", "min-width: 14rem; margin-inline-start: 0.5rem;");
            builder.CloseComponent();
        }
    }

    private void InsertText(string? snippet)
    {
        if (string.IsNullOrEmpty(snippet))
        {
            return;
        }

        var text = Text(Property) ?? string.Empty;
        Set(Property, JsonValue.Create(text + (text.Length == 0 || text.EndsWith(' ') || text.EndsWith('\n') ? string.Empty : " ") + snippet));
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

/// <summary>A number: a spinner by default; the editor setting picks a slider (<c>Range</c>, <c>Slider</c>) or a list of the allowed values (<c>Select</c>) within the field's minimum and maximum.</summary>
[BlazorComponent("NumericFieldEditor", DisplayName = "Number", Category = "Editors", Icon = "pin", Edits = ["NumericField"])]
public sealed class NumericFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var node = Value?["Value"];
        decimal? value = node is null ? null : Convert.ToDecimal(node.GetValue<object>(), CultureInfo.InvariantCulture);
        var minimum = Decimal(Setting("NumericFieldSettings", "Minimum"));
        var maximum = Decimal(Setting("NumericFieldSettings", "Maximum"));
        var scale = Setting("NumericFieldSettings", "Scale") is JsonValue scaleValue && scaleValue.TryGetValue<int>(out var digits) ? Math.Clamp(digits, 0, 10) : 0;
        var step = scale == 0 ? 1m : (decimal)Math.Pow(10, -scale);
        var changed = EventCallback.Factory.Create<decimal?>(this, updated => Set("Value", updated is null ? null : JsonValue.Create(updated)));

        if ((IsMode("Range") || IsMode("Slider")) && minimum is { } low && maximum is { } high)
        {
            builder.OpenComponent<GroveSlider<decimal?>>(10);
            builder.AddComponentParameter(11, "Value", value ?? low);
            builder.AddComponentParameter(12, "ValueChanged", changed);
            builder.AddComponentParameter(13, "Min", low);
            builder.AddComponentParameter(14, "Max", high);
            builder.AddComponentParameter(15, "Step", step.ToString(CultureInfo.InvariantCulture));
            builder.AddComponentParameter(16, "Disabled", Disabled);
            builder.AddComponentParameter(17, "Style", "width: 100%;");
            builder.CloseComponent();
            builder.OpenElement(18, "span");
            builder.AddAttribute(19, "class", "grove-editor__value");
            builder.AddContent(20, (value ?? low).ToString(CultureInfo.CurrentCulture));
            builder.CloseElement();
            return;
        }

        if (IsMode("Select") && minimum is { } first && maximum is { } last && (last - first) / step <= 1000)
        {
            var options = new List<decimal>();
            for (var option = first; option <= last; option += step)
            {
                options.Add(option);
            }

            builder.OpenComponent<GroveDropDown<decimal?>>(30);
            builder.AddComponentParameter(31, "Data", options);
            builder.AddComponentParameter(32, "Value", value);
            builder.AddComponentParameter(33, "ValueChanged", changed);
            builder.AddComponentParameter(34, "AllowClear", Field?.Required != true);
            builder.AddComponentParameter(35, "Disabled", Disabled);
            builder.AddComponentParameter(36, "Style", "width: 100%;");
            builder.CloseComponent();
            return;
        }

        builder.OpenComponent<GroveNumeric<decimal?>>(0);
        builder.AddComponentParameter(1, "Value", value);
        builder.AddComponentParameter(2, "ValueChanged", changed);
        builder.AddComponentParameter(3, "Disabled", Disabled);
        builder.AddComponentParameter(4, "Style", "width: 100%;");
        builder.AddComponentParameter(5, "Min", minimum);
        builder.AddComponentParameter(6, "Max", maximum);
        builder.AddComponentParameter(7, "Step", step.ToString(CultureInfo.InvariantCulture));
        builder.AddComponentParameter(8, "Placeholder", Setting("NumericFieldSettings", "Placeholder")?.ToString());
        builder.CloseComponent();
    }

    private static decimal? Decimal(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<decimal>(out var number) ? number
        : node is JsonValue text && decimal.TryParse(text.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed
        : null;
}

[BlazorComponent("BooleanFieldEditor", DisplayName = "Yes/no", Category = "Editors", Icon = "check_box", Edits = ["BooleanField"])]
public sealed class BooleanFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var value = Value?["Value"]?.GetValue<bool>() == true;
        var changed = EventCallback.Factory.Create<bool>(this, updated => Set("Value", JsonValue.Create(updated)));
        if (IsMode("Switch"))
        {
            builder.OpenComponent<GroveSwitch>(10);
            builder.AddComponentParameter(11, nameof(GroveSwitch.Value), value);
            builder.AddComponentParameter(12, nameof(GroveSwitch.ValueChanged), changed);
            builder.AddComponentParameter(13, nameof(GroveSwitch.Disabled), Disabled);
            builder.CloseComponent();
            return;
        }

        builder.OpenComponent<GroveCheckBox<bool>>(0);
        builder.AddComponentParameter(1, "Value", value);
        builder.AddComponentParameter(2, "ValueChanged", changed);
        builder.AddComponentParameter(3, "Disabled", Disabled);
        builder.CloseComponent();
    }
}

/// <summary>
/// A date, a date and time, or a time: the date fields (the <c>Localized</c> editor setting shows
/// the culture's date format), a time field, and the scheduled UTC date of a part that schedules
/// its item (publish later, archive later).
/// </summary>
[BlazorComponent("DateFieldEditor", DisplayName = "Date", Category = "Editors", Icon = "event", Edits = ["DateField", "DateTimeField", "TimeField", "PublishLaterPart", "ArchiveLaterPart"])]
public sealed class DateFieldEditor : FieldEditorBase
{
    private string Property => ValueType switch
    {
        "PublishLaterPart" => "ScheduledPublishUtc",
        "ArchiveLaterPart" => "ScheduledArchiveUtc",
        "CommonPart" => "CreatedUtc",
        _ => "Value",
    };

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var property = Property;
        if (ValueType == "TimeField")
        {
            var stored = Value?[property];
            TimeSpan? time = stored is JsonValue timeValue && TimeSpan.TryParse(timeValue.ToString(), CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
            builder.OpenComponent<GroveTimeSpanPicker<TimeSpan?>>(10);
            builder.AddComponentParameter(11, "Value", time);
            builder.AddComponentParameter(12, "ValueChanged", EventCallback.Factory.Create<TimeSpan?>(this, changed => Set(property, changed is null ? null : JsonValue.Create(changed.Value.ToString("c", CultureInfo.InvariantCulture)))));
            builder.AddComponentParameter(13, "Disabled", Disabled);
            builder.AddComponentParameter(14, "Min", TimeSpan.Zero);
            builder.AddComponentParameter(15, "Max", new TimeSpan(23, 59, 59));
            builder.CloseComponent();
            return;
        }

        var withTime = ValueType is "DateTimeField" or "PublishLaterPart" or "ArchiveLaterPart" or "CommonPart";
        builder.OpenComponent<GroveDatePicker<DateTime?>>(0);
        builder.AddComponentParameter(1, "Value", ContentEditorContext.ReadDate(Value?[property]));
        builder.AddComponentParameter(2, "ValueChanged", EventCallback.Factory.Create<DateTime?>(this, changed => Set(property, changed is null ? null : JsonValue.Create(changed))));
        builder.AddComponentParameter(3, "Disabled", Disabled);
        builder.AddComponentParameter(4, "Style", "width: 100%;");
        builder.AddComponentParameter(5, "ShowTime", withTime);
        builder.AddComponentParameter(6, "AllowClear", true);
        if (IsMode("Localized"))
        {
            var format = CultureInfo.CurrentCulture.DateTimeFormat;
            builder.AddComponentParameter(7, "DateFormat", withTime ? format.ShortDatePattern + " " + format.ShortTimePattern : format.ShortDatePattern);
        }

        builder.CloseComponent();
    }
}

/// <summary>
/// Localization sets (with Content Localization): the sets the field holds as chips (removed,
/// moved) and a search over the field's candidates (Grove.ContentFields'
/// <c>api/grove/content-fields/localization-sets</c>). Content items are picked with the option
/// picker over the content items source (Grove.OptionPicker).
/// </summary>
[BlazorComponent("LocalizationSetPickerFieldEditor", DisplayName = "Localized content items", Category = "Editors", Icon = "translate", Edits = ["LocalizationSetContentPickerField"])]
public sealed class LocalizationSetPickerFieldEditor : FieldEditorBase
{
    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    private readonly Dictionary<string, PickerItem> _known = new(StringComparer.Ordinal);
    private IReadOnlyList<PickerItem> _results = [];
    private string? _query;
    private bool _loaded;
    private string? _error;

    private const string Property = "LocalizationSets";

    private const string Endpoint = "api/grove/content-fields/localization-sets";

    private bool Multiple => Setting("LocalizationSetContentPickerFieldSettings", "Multiple") is JsonValue value && value.TryGetValue<bool>(out var multiple) && multiple;

    private IReadOnlyList<string> Selected => Value?[Property] is JsonArray array
        ? [.. array.Select(id => id?.ToString()).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!)]
        : [];

    protected override async Task OnParametersSetAsync()
    {
        if (_loaded || Context?.Http is null || FieldName is null)
        {
            return;
        }

        _loaded = true;
        var selected = Selected;
        await LoadAsync(selected.Count > 0 ? "&ids=" + Uri.EscapeDataString(string.Join(',', selected)) : null, remember: true);
    }

    private async Task LoadAsync(string? query, bool remember)
    {
        if (Context?.Http is not { } http)
        {
            return;
        }

        try
        {
            var uri = $"{Endpoint}?part={Uri.EscapeDataString(PartName)}&field={Uri.EscapeDataString(FieldName ?? string.Empty)}{query}";
            var results = await http.GetFromJsonAsync<PickerResults>(uri, s_web);
            foreach (var item in results?.Items ?? [])
            {
                _known[item.Id] = item;
            }

            if (!remember)
            {
                _results = results?.Items ?? [];
            }

            _error = null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _error = "The items could not be loaded.";
        }
    }

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var selected = Selected;
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-content-picker");

        builder.OpenElement(2, "ul");
        builder.AddAttribute(3, "class", "grove-content-picker__selected");
        for (var index = 0; index < selected.Count; index++)
        {
            var position = index;
            var id = selected[index];
            var known = _known.GetValueOrDefault(id);
            builder.OpenElement(4, "li");
            builder.SetKey(id);
            builder.AddContent(9, known?.DisplayText ?? id);

            if (known is { HasPublished: false })
            {
                builder.AddContent(10, " (draft)");
            }

            Button(builder, 11, "arrow_upward", "Move up", Disabled || position == 0, () => Write(Swap(selected, position, -1)));
            Button(builder, 12, "arrow_downward", "Move down", Disabled || position == selected.Count - 1, () => Write(Swap(selected, position, 1)));
            Button(builder, 13, "close", "Remove", Disabled, () => Write([.. selected.Where(other => other != id)]));
            builder.CloseElement();
        }

        builder.CloseElement();

        if (!Disabled)
        {
            builder.OpenElement(20, "div");
            builder.AddAttribute(21, "class", "grove-editor__pair");
            builder.OpenComponent<GroveTextBox>(22);
            builder.AddComponentParameter(23, nameof(GroveTextBox.Value), _query);
            builder.AddComponentParameter(24, nameof(GroveTextBox.Placeholder), "Search");
            builder.AddComponentParameter(25, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, async query =>
            {
                _query = query;
                await LoadAsync("&query=" + Uri.EscapeDataString(query ?? string.Empty), remember: false);
            }));
            builder.CloseComponent();
            builder.CloseElement();

            builder.OpenElement(40, "ul");
            builder.AddAttribute(41, "class", "grove-content-picker__results");
            foreach (var result in _results.Where(result => !selected.Contains(result.Id)))
            {
                builder.OpenElement(42, "li");
                builder.SetKey(result.Id);
                builder.OpenElement(43, "button");
                builder.AddAttribute(44, "type", "button");
                builder.AddAttribute(45, "class", "rz-button rz-light rz-button-xs");
                builder.AddAttribute(46, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => Write(Multiple ? [.. selected, result.Id] : [result.Id])));
                builder.AddContent(47, (result.DisplayText ?? result.Id) + (result.ContentType is { } type ? $" ({type})" : string.Empty));
                builder.CloseElement();
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        if (_error is not null)
        {
            builder.OpenElement(50, "div");
            builder.AddAttribute(51, "class", "grove-editor__error rz-color-danger");
            builder.AddContent(52, _error);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    private static IReadOnlyList<string> Swap(IReadOnlyList<string> ids, int index, int offset)
    {
        var list = ids.ToList();
        (list[index], list[index + offset]) = (list[index + offset], list[index]);
        return list;
    }

    private void Write(IReadOnlyList<string> ids) =>
        Set(Property, new JsonArray([.. ids.Select(id => (JsonNode?)JsonValue.Create(id))]));

    private void Button(RenderTreeBuilder builder, int sequence, string icon, string title, bool disabled, Action click)
    {
        builder.OpenComponent<GroveButton>(sequence);
        builder.AddComponentParameter(sequence + 100, nameof(GroveButton.Icon), icon);
        builder.AddComponentParameter(sequence + 101, nameof(GroveButton.Variant), Variant.Text);
        builder.AddComponentParameter(sequence + 102, nameof(GroveButton.Size), ButtonSize.ExtraSmall);
        builder.AddComponentParameter(sequence + 103, nameof(GroveButton.Disabled), disabled);
        builder.AddComponentParameter(sequence + 104, "title", title);
        builder.AddComponentParameter(sequence + 105, nameof(GroveButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, click));
        builder.CloseComponent();
    }

    /// <summary>A candidate as the localization set picker API serves it.</summary>
    public sealed record PickerItem(string Id, string? DisplayText, string? ContentType, bool HasPublished, bool IsEditable);

    public sealed record PickerType(string Name, string DisplayName);

    public sealed record PickerResults(IReadOnlyList<PickerItem> Items, IReadOnlyList<PickerType> CreatableTypes);
}

/// <summary>
/// A multi-text field: the field's own options, as check boxes, or a picker with the
/// <c>Picker</c> editor setting. (An option picker field's editor is Grove.OptionPicker's.)
/// </summary>
[BlazorComponent("MultiTextFieldEditor", DisplayName = "Choices", Category = "Editors", Icon = "checklist", Edits = ["MultiTextField"])]
public sealed class MultiTextFieldEditor : FieldEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder) => RenderMultiText(builder);

    private void RenderMultiText(RenderTreeBuilder builder)
    {
        var options = Setting("MultiTextFieldSettings", "Options") is JsonArray array
            ? array.Select(option => new Forms.SelectOption(option?["name"]?.ToString() ?? option?["value"]?.ToString() ?? string.Empty, option?["value"]?.ToString() ?? string.Empty)).ToList()
            : [];
        IEnumerable<string> selected = Value?["Values"] is JsonArray values
            ? [.. values.Select(value => value?.ToString()).Where(value => !string.IsNullOrEmpty(value)).Select(value => value!)]
            : [];
        var changed = EventCallback.Factory.Create<IEnumerable<string>>(this, updated =>
            Set("Values", new JsonArray([.. (updated ?? []).Select(value => (JsonNode?)JsonValue.Create(value))])));

        if (IsMode("Picker"))
        {
            builder.OpenComponent<GroveDropDown<IEnumerable<string>>>(20);
            builder.AddComponentParameter(21, "Data", options);
            builder.AddComponentParameter(22, "TextProperty", nameof(Forms.SelectOption.Text));
            builder.AddComponentParameter(23, "ValueProperty", nameof(Forms.SelectOption.Value));
            builder.AddComponentParameter(24, "Multiple", true);
            builder.AddComponentParameter(25, "Chips", true);
            builder.AddComponentParameter(26, "Value", selected);
            builder.AddComponentParameter(27, "ValueChanged", changed);
            builder.AddComponentParameter(28, "Disabled", Disabled);
            builder.AddComponentParameter(29, "Style", "width: 100%;");
            builder.CloseComponent();
            return;
        }

        builder.OpenComponent<GroveCheckBoxList<string>>(30);
        builder.AddComponentParameter(31, "Data", options);
        builder.AddComponentParameter(32, "TextProperty", nameof(Forms.SelectOption.Text));
        builder.AddComponentParameter(33, "ValueProperty", nameof(Forms.SelectOption.Value));
        builder.AddComponentParameter(34, "Value", selected);
        builder.AddComponentParameter(35, "ValueChanged", changed);
        builder.AddComponentParameter(36, "Disabled", Disabled);
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
        builder.OpenComponent<CodeEditor>(0);
        builder.AddComponentParameter(1, nameof(CodeEditor.Value), Value?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "{}");
        builder.AddComponentParameter(2, nameof(CodeEditor.Changed), EventCallback.Factory.Create<string?>(this, text => Replace(text ?? string.Empty)));
        builder.AddComponentParameter(3, nameof(CodeEditor.Disabled), Disabled);
        builder.AddComponentParameter(4, nameof(CodeEditor.Language), "json");
        builder.AddComponentParameter(5, nameof(CodeEditor.Height), "10rem");
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
