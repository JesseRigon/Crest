using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Display.Components.Forms;

/// <summary>
/// A one-line input (docs/widgets.md, the Input widget): text, email, number, date, telephone,
/// URL, password or a checkbox. A number submits as a number, a checkbox as a boolean, the rest
/// as text.
/// </summary>
[BlazorComponent("TextInput", DisplayName = "Input", Category = "Forms", Icon = "input")]
public sealed class TextInput : FormInputBase
{
    private string? _text;
    private bool _checked;
    private bool _initialized;

    [Parameter]
    [ComponentParameter(Group = "Content", Choices = ["text", "email", "number", "date", "tel", "url", "password", "checkbox"])]
    public string Type { get; set; } = "text";

    /// <summary>The initial value ("true" checks a checkbox).</summary>
    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Default { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Placeholder { get; set; }

    private bool IsCheckbox => string.Equals(Type, "checkbox", StringComparison.OrdinalIgnoreCase);

    private bool IsNumber => string.Equals(Type, "number", StringComparison.OrdinalIgnoreCase);

    protected override JsonNode? CurrentValue =>
        IsCheckbox ? JsonValue.Create(_checked)
        : string.IsNullOrEmpty(_text) ? null
        : IsNumber ? (double.TryParse(_text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? JsonValue.Create(number) : JsonValue.Create(_text))
        : JsonValue.Create(_text);

    protected override void OnParametersSet()
    {
        if (!_initialized)
        {
            _text = Default;
            _checked = bool.TryParse(Default, out var value) && value;
            _initialized = true;
        }

        base.OnParametersSet();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => RenderField(builder, IsCheckbox ? "checkbox" : "input", control =>
    {
        control.OpenElement(0, "input");
        AddControlAttributes(control);
        control.AddAttribute(1, "class", "grove-form-field__control");
        control.AddAttribute(2, "type", Choice(Type));
        if (IsCheckbox)
        {
            control.AddAttribute(3, "checked", _checked);
            control.AddAttribute(4, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, args =>
            {
                _checked = args.Value is bool value ? value : bool.TryParse(args.Value?.ToString(), out var parsed) && parsed;
                return PublishAsync();
            }));
        }
        else
        {
            control.AddAttribute(5, "placeholder", Placeholder);
            control.AddAttribute(6, "value", _text);
            control.AddAttribute(7, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, args =>
            {
                _text = args.Value?.ToString();
                return PublishAsync();
            }));
        }

        control.CloseElement();
    });

    private static readonly HashSet<string> s_types = new(StringComparer.OrdinalIgnoreCase) { "text", "email", "number", "date", "tel", "url", "password", "checkbox" };

    private static string Choice(string? type) => type is not null && s_types.Contains(type) ? type.ToLowerInvariant() : "text";
}

/// <summary>A multi-line input (docs/widgets.md, the TextArea widget).</summary>
[BlazorComponent("TextArea", DisplayName = "Text area", Category = "Forms", Icon = "notes")]
public sealed class TextArea : FormInputBase
{
    private string? _text;
    private bool _initialized;

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Default { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Placeholder { get; set; }

    [Parameter]
    public int Rows { get; set; } = 4;

    protected override JsonNode? CurrentValue => string.IsNullOrEmpty(_text) ? null : JsonValue.Create(_text);

    protected override void OnParametersSet()
    {
        if (!_initialized)
        {
            _text = Default;
            _initialized = true;
        }

        base.OnParametersSet();
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => RenderField(builder, "textarea", control =>
    {
        control.OpenElement(0, "textarea");
        AddControlAttributes(control);
        control.AddAttribute(1, "class", "grove-form-field__control");
        control.AddAttribute(2, "rows", Math.Clamp(Rows, 1, 50));
        control.AddAttribute(3, "placeholder", Placeholder);
        control.AddAttribute(4, "value", _text);
        control.AddAttribute(5, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, args =>
        {
            _text = args.Value?.ToString();
            return PublishAsync();
        }));
        control.CloseElement();
    });
}

/// <summary>
/// A choice among options (docs/widgets.md, the Select widget), as a dropdown or radio buttons.
/// <see cref="Options"/> takes <c>{ text, value }</c> objects or plain strings (the value is the text).
/// </summary>
[BlazorComponent("Select", DisplayName = "Select", Category = "Forms", Icon = "arrow_drop_down_circle")]
public sealed class Select : FormInputBase
{
    private string? _selected;
    private bool _initialized;

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public IReadOnlyList<SelectOption>? Options { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Default { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content", Choices = ["Dropdown", "Radio"])]
    public string Editor { get; set; } = "Dropdown";

    /// <summary>The empty first entry of a dropdown ("Choose…"); none when absent.</summary>
    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Placeholder { get; set; }

    protected override JsonNode? CurrentValue => string.IsNullOrEmpty(_selected) ? null : JsonValue.Create(_selected);

    protected override void OnParametersSet()
    {
        if (!_initialized)
        {
            _selected = Default;
            _initialized = true;
        }

        base.OnParametersSet();
    }

    private EventCallback<ChangeEventArgs> OnChange => EventCallback.Factory.Create<ChangeEventArgs>(this, args =>
    {
        _selected = args.Value?.ToString();
        return PublishAsync();
    });

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var options = Options ?? [];
        if (string.Equals(Editor, "Radio", StringComparison.OrdinalIgnoreCase))
        {
            RenderField(builder, "radio", control =>
            {
                control.OpenElement(0, "div");
                control.AddAttribute(1, "class", "grove-form-field__options");
                control.AddAttribute(2, "role", "radiogroup");
                control.AddAttribute(3, "id", Id);
                var index = 0;
                foreach (var option in options)
                {
                    var optionId = $"{Id}-{index++}";
                    control.OpenElement(4, "label");
                    control.AddAttribute(5, "class", "grove-form-field__option");
                    control.AddAttribute(6, "for", optionId);
                    control.OpenElement(7, "input");
                    control.AddAttribute(8, "type", "radio");
                    control.AddAttribute(9, "id", optionId);
                    control.AddAttribute(10, "name", Name);
                    control.AddAttribute(11, "value", option.Value);
                    control.AddAttribute(12, "checked", string.Equals(option.Value, _selected, StringComparison.Ordinal));
                    control.AddAttribute(13, "onchange", OnChange);
                    control.CloseElement();
                    control.AddContent(14, option.Text);
                    control.CloseElement();
                }

                control.CloseElement();
            });
            return;
        }

        RenderField(builder, "select", control =>
        {
            control.OpenElement(0, "select");
            AddControlAttributes(control);
            control.AddAttribute(1, "class", "grove-form-field__control");
            control.AddAttribute(2, "value", _selected);
            control.AddAttribute(3, "onchange", OnChange);
            if (Placeholder is not null || !Required)
            {
                control.OpenElement(4, "option");
                control.AddAttribute(5, "value", string.Empty);
                control.AddContent(6, Placeholder);
                control.CloseElement();
            }

            foreach (var option in options)
            {
                control.OpenElement(7, "option");
                control.AddAttribute(8, "value", option.Value);
                control.AddAttribute(9, "selected", string.Equals(option.Value, _selected, StringComparison.Ordinal));
                control.AddContent(10, option.Text);
                control.CloseElement();
            }

            control.CloseElement();
        });
    }
}

/// <summary>One option of a <see cref="Select"/>: read from <c>{ "text": "…", "value": "…" }</c> or a plain string.</summary>
[JsonConverter(typeof(SelectOptionConverter))]
public sealed record SelectOption(string Text, string Value);

internal sealed class SelectOptionConverter : JsonConverter<SelectOption>
{
    public override SelectOption? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False)
        {
            var text = reader.TokenType == JsonTokenType.String ? reader.GetString() ?? string.Empty : JsonNode.Parse(ref reader)!.ToJsonString();
            return new SelectOption(text, text);
        }

        if (JsonNode.Parse(ref reader) is not JsonObject json)
        {
            throw new JsonException("An option is a string or an object with text and value.");
        }

        var value = Member(json, "value");
        var label = Member(json, "text") ?? value ?? string.Empty;
        return new SelectOption(label, value ?? label);
    }

    public override void Write(Utf8JsonWriter writer, SelectOption value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("text", value.Text);
        writer.WriteString("value", value.Value);
        writer.WriteEndObject();
    }

    private static string? Member(JsonObject json, string name) =>
        json.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value is JsonValue value
            ? value.TryGetValue<string>(out var text) ? text : value.ToJsonString()
            : null;
}

/// <summary>Every error of the enclosing form (docs/widgets.md, the ValidationSummary widget).</summary>
[BlazorComponent("ValidationSummary", DisplayName = "Validation summary", Category = "Forms", Icon = "error")]
public sealed class ValidationSummary : ComponentBase, IDisposable
{
    private FormContext? _subscribed;

    [CascadingParameter]
    public FormContext? Form { get; set; }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, Form))
        {
            if (_subscribed is not null)
            {
                _subscribed.Changed -= OnChanged;
            }

            _subscribed = Form;
            if (_subscribed is not null)
            {
                _subscribed.Changed += OnChanged;
            }
        }
    }

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var errors = Form?.AllErrors().ToList() ?? [];
        if (errors.Count == 0)
        {
            return;
        }

        builder.OpenElement(0, "ul");
        builder.AddAttribute(1, "class", "grove-validation-summary");
        builder.AddAttribute(2, "role", "alert");
        foreach (var error in errors)
        {
            builder.OpenElement(3, "li");
            builder.AddContent(4, error);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    public void Dispose()
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnChanged;
        }
    }
}
