using System.Text.Json.Nodes;
using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Crest.Components.Forms;

/// <summary>
/// A form a template places (docs/widgets.md, Forms): it collects the values of the inputs inside
/// it into one object and publishes it as <see cref="Submitted"/>. A template runs a workflow on
/// it through the UI action contract, so the arguments are checked by name and type on save and
/// on submit; antiforgery comes from the action endpoint:
/// <code>
/// {% compose "Form", id: "contact" %}
///   {% on "submitted" %}{% run_workflow "contact", email: event.email, message: event.message %}{% endon %}
///   {% component "TextInput", name: "email", type: "email", label: "Email", required: true %}
///   {% component "TextArea", name: "message", label: "Message" %}
///   {% component "ValidationSummary" %}
///   {% component "Button", text: "Send", button_type: "Submit" %}
/// {% endcompose %}
/// </code>
/// The errors the run returns (by input name) are shown beside each input and in the summary.
/// </summary>
[BlazorComponent("Form", DisplayName = "Form", Category = "Forms", Icon = "dynamic_form")]
public sealed class Form : ComponentBase, IDisposable
{
    private readonly FormContext _context = new();
    private DisplayState? _subscribed;

    /// <summary>Filled by the renderer: the errors of this node's actions are kept under it.</summary>
    [Parameter]
    [ComponentParameter(Hidden = true, ReadOnly = true)]
    public string? InstanceId { get; set; }

    [Parameter]
    [ComponentParameter(Advanced = true)]
    public string? Class { get; set; }

    [Parameter]
    [ComponentZone]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>The inputs' values by name, published when the form is submitted.</summary>
    [Parameter]
    [ComponentOutput(DisplayName = "Submitted")]
    public EventCallback<JsonObject> Submitted { get; set; }

    [CascadingParameter]
    public DisplayState? State { get; set; }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, State))
        {
            if (_subscribed is not null)
            {
                _subscribed.Changed -= OnStateChanged;
            }

            _subscribed = State;
            if (_subscribed is not null)
            {
                _subscribed.Changed += OnStateChanged;
            }
        }

        OnStateChanged();
    }

    private void OnStateChanged() =>
        _context.SetErrors(State is not null && InstanceId is not null ? State.GetErrors(InstanceId) : null);

    private async Task SubmitAsync()
    {
        _context.SetErrors(null);
        if (_context.CheckRequired() is { Count: > 0 } missing)
        {
            _context.SetErrors(missing);
            return;
        }

        await Submitted.InvokeAsync(_context.Values());
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<FormContext>>(0);
        builder.AddComponentParameter(1, nameof(CascadingValue<FormContext>.Value), _context);
        builder.AddComponentParameter(2, nameof(CascadingValue<FormContext>.IsFixed), true);
        builder.AddComponentParameter(3, nameof(CascadingValue<FormContext>.ChildContent), (RenderFragment)(form =>
        {
            form.OpenElement(4, "form");
            form.AddAttribute(5, "class", string.IsNullOrEmpty(Class) ? "crest-form" : "crest-form " + Class);
            form.AddAttribute(6, "novalidate", true);
            form.AddAttribute(7, "onsubmit", EventCallback.Factory.Create(this, SubmitAsync));
            form.AddEventPreventDefaultAttribute(8, "onsubmit", true);
            form.AddContent(9, ChildContent);
            form.CloseElement();
        }));
        builder.CloseComponent();
    }

    public void Dispose()
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnStateChanged;
        }
    }
}

/// <summary>
/// What a <see cref="Form"/> cascades to its inputs: each input registers its name and current
/// value, and reads the errors for its name.
/// </summary>
public sealed class FormContext
{
    private readonly Dictionary<string, FormField> _fields = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyDictionary<string, IReadOnlyList<string>> _errors = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Raised when the errors change.</summary>
    public event Action? Changed;

    public void Register(FormField field) => _fields[field.Name] = field;

    public void Unregister(FormField field)
    {
        if (_fields.TryGetValue(field.Name, out var registered) && ReferenceEquals(registered, field))
        {
            _fields.Remove(field.Name);
        }
    }

    public IReadOnlyList<string> Errors(string name) => _errors.TryGetValue(name, out var errors) ? errors : [];

    /// <summary>Every error, the form's own (no input) first.</summary>
    public IEnumerable<string> AllErrors() =>
        _errors.OrderBy(pair => pair.Key.Length == 0 ? 0 : 1).SelectMany(pair => pair.Value);

    public void SetErrors(IReadOnlyDictionary<string, IReadOnlyList<string>>? errors)
    {
        errors ??= new Dictionary<string, IReadOnlyList<string>>();
        if (errors.Count == 0 && _errors.Count == 0)
        {
            return;
        }

        // Input names match without case, as the workflow contract checks them.
        _errors = errors.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        Changed?.Invoke();
    }

    /// <summary>The required inputs without a value, checked before anything is sent.</summary>
    public Dictionary<string, IReadOnlyList<string>> CheckRequired() =>
        _fields.Values
            .Where(field => field.Required && IsEmpty(field.Value()))
            .ToDictionary(field => field.Name, field => (IReadOnlyList<string>)[$"{field.Label ?? field.Name} is required."], StringComparer.OrdinalIgnoreCase);

    /// <summary>The submitted object: each input's value by its name.</summary>
    public JsonObject Values()
    {
        var values = new JsonObject();
        foreach (var field in _fields.Values)
        {
            values[field.Name] = field.Value();
        }

        return values;
    }

    private static bool IsEmpty(JsonNode? value) =>
        value is null || (value is JsonValue text && text.TryGetValue<string>(out var s) && string.IsNullOrWhiteSpace(s))
        || (value is JsonValue flag && flag.TryGetValue<bool>(out var b) && !b);
}

/// <summary>One input as its form sees it.</summary>
public sealed record FormField(string Name, string? Label, bool Required, Func<JsonNode?> Value);

/// <summary>
/// The base of the form inputs: a name the form submits the value under, a label, the required
/// flag, the errors for its name under it, and its value published as the <c>Value</c> output so
/// other nodes may bind to it (a field shown by another's value).
/// </summary>
public abstract class FormInputBase : ComponentBase, IDisposable
{
    private FormContext? _subscribed;
    private FormField? _field;

    /// <summary>The name the form submits the value under (the workflow input's name).</summary>
    [Parameter, EditorRequired]
    [ComponentParameter(Group = "Content", Required = true)]
    public string Name { get; set; } = string.Empty;

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Label { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public bool Required { get; set; }

    [Parameter]
    [ComponentParameter(Advanced = true)]
    public string? Class { get; set; }

    /// <summary>
    /// Whether the input is shown (docs/widgets.md, visibility rules): a template binds it to a
    /// comparison on another input's value (<c>visible: nodes.country.value == "US"</c>). A
    /// hidden input is not registered with the form, so it is neither required nor submitted.
    /// </summary>
    [Parameter]
    [ComponentParameter(Group = "Content")]
    public bool Visible { get; set; } = true;

    /// <summary>The current value, published on every change.</summary>
    [Parameter]
    [ComponentOutput(DisplayName = "Value")]
    public EventCallback<JsonNode?> Value { get; set; }

    [CascadingParameter]
    public FormContext? Form { get; set; }

    protected string Id => "form-" + Name;

    protected IReadOnlyList<string> Errors => Form?.Errors(Name) ?? [];

    /// <summary>The value the form submits.</summary>
    protected abstract JsonNode? CurrentValue { get; }

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, Form))
        {
            Detach();
            _subscribed = Form;
            if (_subscribed is not null)
            {
                _subscribed.Changed += OnFormChanged;
            }
        }

        if (_subscribed is null)
        {
            return;
        }

        if (!Visible)
        {
            if (_field is not null)
            {
                _subscribed.Unregister(_field);
                _field = null;
            }

            return;
        }

        if (_field is null || _field.Name != Name || _field.Label != Label || _field.Required != Required)
        {
            if (_field is not null)
            {
                _subscribed.Unregister(_field);
            }

            _field = new FormField(Name, Label, Required, () => CurrentValue);
            _subscribed.Register(_field);
        }
    }

    /// <summary>Publishes the value after the viewer changed it.</summary>
    protected Task PublishAsync() => Value.InvokeAsync(CurrentValue);

    private void OnFormChanged() => _ = InvokeAsync(StateHasChanged);

    /// <summary>The label, the control (<paramref name="control"/>) and the errors, in one block.</summary>
    protected void RenderField(RenderTreeBuilder builder, string kind, RenderFragment control)
    {
        if (!Visible)
        {
            return;
        }

        var errors = Errors;
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", $"crest-form-field crest-form-field--{kind}" + (errors.Count > 0 ? " crest-form-field--invalid" : string.Empty) + (string.IsNullOrEmpty(Class) ? string.Empty : " " + Class));
        if (!string.IsNullOrEmpty(Label))
        {
            builder.OpenElement(2, "label");
            builder.AddAttribute(3, "for", Id);
            builder.AddContent(4, Label);
            if (Required)
            {
                builder.OpenElement(5, "span");
                builder.AddAttribute(6, "class", "crest-form-field__required");
                builder.AddAttribute(7, "aria-hidden", "true");
                builder.AddContent(8, " *");
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.AddContent(9, control);
        foreach (var error in errors)
        {
            builder.OpenElement(10, "div");
            builder.AddAttribute(11, "class", "crest-form-field__error");
            builder.AddAttribute(12, "id", Id + "-error");
            builder.AddAttribute(13, "role", "alert");
            builder.AddContent(14, error);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    /// <summary>The attributes every control carries: id, name, required, the error link.</summary>
    protected void AddControlAttributes(RenderTreeBuilder builder)
    {
        builder.AddAttribute(100, "id", Id);
        builder.AddAttribute(101, "name", Name);
        builder.AddAttribute(102, "required", Required);
        builder.AddAttribute(103, "aria-invalid", Errors.Count > 0 ? "true" : null);
        builder.AddAttribute(104, "aria-describedby", Errors.Count > 0 ? Id + "-error" : null);
    }

    private void Detach()
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnFormChanged;
            if (_field is not null)
            {
                _subscribed.Unregister(_field);
            }
        }

        _field = null;
    }

    public void Dispose() => Detach();
}
