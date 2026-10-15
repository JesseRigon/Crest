using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Display.Components.Primitives;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Display.Components.Editors;

/// <summary>A condition type of the rule editor's catalog (Grove.Display.Rules' <c>api/grove/rules/conditions</c>).</summary>
public sealed record RuleConditionTypeModel(string Name, string DisplayName, string Description, string Category, JsonObject Template, string[]? Choices);

/// <summary>An operator a string condition compares with.</summary>
public sealed record RuleOperatorModel(string Name, string DisplayName, bool Negates, JsonObject Template);

public sealed record RuleCatalogModel(RuleConditionTypeModel[] Conditions, RuleOperatorModel[] Operators);

/// <summary>
/// The rule being edited (cascaded to the condition editors): the condition types and operators
/// the server offers, each condition's problem by its id, and the change notification the
/// editors raise when they write into a condition.
/// </summary>
public sealed class RuleEditorContext(RuleCatalogModel catalog)
{
    public RuleCatalogModel Catalog { get; } = catalog;

    public Dictionary<string, string> Errors { get; } = new(StringComparer.Ordinal);

    public bool Disabled { get; set; }

    public event Action? Changed;

    public RuleConditionTypeModel? Type(string? name) =>
        Catalog.Conditions.FirstOrDefault(type => string.Equals(type.Name, name, StringComparison.Ordinal));

    /// <summary>A new condition of <paramref name="name"/>, from the server's template, with an id of its own.</summary>
    public JsonObject Create(string name)
    {
        var condition = Type(name)?.Template.DeepClone() as JsonObject ?? new JsonObject { ["Name"] = name };
        condition["ConditionId"] = Guid.NewGuid().ToString("n");
        return condition;
    }

    public void NotifyChanged() => Changed?.Invoke();

    public static string? Id(JsonObject? condition) =>
        condition?["ConditionId"] is JsonValue id && id.TryGetValue<string>(out var text) ? text : null;

    public static string? Name(JsonObject? condition) =>
        condition?["Name"] is JsonValue name && name.TryGetValue<string>(out var text) ? text : null;
}

/// <summary>
/// A rule's conditions (Grove.Display.Rules; a layer's rule on the Layers page): a list of
/// conditions, each edited by the registered component that <c>Edits</c> its type's name (the
/// condition editors below, or a module's own), a group nesting its own list; conditions are
/// added from the types the enabled features registered, reordered and removed. The JSON is the
/// rule's stored form (each condition's <c>Name</c>, its <c>$type</c> and values), so what the page
/// saves is what the recipe step reads. <see cref="CheckAsync"/> asks the server to check the
/// conditions (a JavaScript condition's script parses and returns a boolean).
/// </summary>
[BlazorComponent("RuleEditor", DisplayName = "Rule", Category = "Editors", Icon = "rule", Edits = ["Rule"])]
public sealed class RuleEditor : ComponentBase, IDisposable
{
    private const string Api = "api/grove/rules/";

    private static readonly JsonSerializerOptions s_web = new(JsonSerializerDefaults.Web);

    private RuleEditorContext? _context;
    private string? _error;

    /// <summary>The conditions, edited in place (the stored JSON array).</summary>
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public JsonArray? Conditions { get; set; }

    [Parameter]
    [ComponentOutput(DisplayName = "Changed")]
    public EventCallback<JsonArray> ConditionsChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [Inject]
    private HttpClient Http { get; set; } = null!;

    [Inject]
    private Theme.GroveApiLocalizer T { get; set; } = null!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var catalog = await Http.GetFromJsonAsync<RuleCatalogModel>(Api + "conditions", s_web);
            _context = new RuleEditorContext(catalog ?? new([], []));
            _context.Changed += OnChanged;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            _error = T["The condition types could not be loaded."];
        }
    }

    protected override void OnParametersSet()
    {
        Conditions ??= [];
        if (_context is not null)
        {
            _context.Disabled = Disabled;
        }
    }

    private void OnChanged()
    {
        _ = ConditionsChanged.InvokeAsync(Conditions!);
        _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>Checks the conditions on the server; their problems show under each. True when there is none.</summary>
    public async Task<bool> CheckAsync()
    {
        if (_context is null)
        {
            return false;
        }

        _context.Errors.Clear();
        try
        {
            using var response = await Http.PostAsJsonAsync(Api + "check", new { Conditions });
            if (!response.IsSuccessStatusCode)
            {
                _error = T["The conditions could not be checked."];
                return false;
            }

            var check = await response.Content.ReadFromJsonAsync<RuleCheckModel>(s_web);
            foreach (var (key, message) in check?.Errors ?? [])
            {
                _context.Errors[key] = message;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _error = T["The conditions could not be checked."];
            return false;
        }

        _error = null;
        StateHasChanged();
        return _context.Errors.Count == 0;
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-rule-editor");
        builder.AddAttribute(2, "data-testid", "rule-editor");
        if (_error is not null)
        {
            builder.OpenElement(3, "div");
            builder.AddAttribute(4, "class", "grove-editor__error rz-color-danger");
            builder.AddContent(5, _error);
            builder.CloseElement();
        }

        if (_context is not null)
        {
            builder.OpenComponent<CascadingValue<RuleEditorContext>>(6);
            builder.AddComponentParameter(7, nameof(CascadingValue<RuleEditorContext>.Value), _context);
            builder.AddComponentParameter(8, nameof(CascadingValue<RuleEditorContext>.IsFixed), true);
            builder.AddComponentParameter(9, nameof(CascadingValue<RuleEditorContext>.ChildContent), (RenderFragment)(child =>
            {
                child.OpenComponent<ConditionList>(0);
                child.AddComponentParameter(1, nameof(ConditionList.Items), Conditions);
                child.CloseComponent();
            }));
            builder.CloseComponent();
        }

        builder.CloseElement();
    }

    public void Dispose()
    {
        if (_context is not null)
        {
            _context.Changed -= OnChanged;
        }
    }

    private sealed record RuleCheckModel(Dictionary<string, string>? Errors);
}

/// <summary>
/// A list of conditions (the rule's, or a group's): each condition in a card with its type's
/// editor, moved up and down and removed; a condition of any offered type added at the end.
/// </summary>
public sealed class ConditionList : ComponentBase
{
    private static readonly Dictionary<string, Type> s_builtIn = new(StringComparer.Ordinal)
    {
        ["AllConditionGroup"] = typeof(ConditionGroupEditor),
        ["AnyConditionGroup"] = typeof(ConditionGroupEditor),
        ["BooleanCondition"] = typeof(BooleanConditionEditor),
        ["HomepageCondition"] = typeof(BooleanConditionEditor),
        ["UrlCondition"] = typeof(StringConditionEditor),
        ["CultureCondition"] = typeof(StringConditionEditor),
        ["RoleCondition"] = typeof(StringConditionEditor),
        ["ContentTypeCondition"] = typeof(StringConditionEditor),
        ["JavascriptCondition"] = typeof(JavascriptConditionEditor),
        ["IsAuthenticatedCondition"] = typeof(FlagConditionEditor),
        ["IsAnonymousCondition"] = typeof(FlagConditionEditor),
    };

    private string? _adding;

    [Parameter, EditorRequired]
    public JsonArray Items { get; set; } = [];

    [CascadingParameter]
    public RuleEditorContext? Rules { get; set; }

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    [Inject]
    private Theme.GroveApiLocalizer T { get; set; } = null!;

    private Type EditorFor(string? name)
    {
        if (name is not null
            && Services.GetService<IComponentRegistry>() is { } registry
            && registry.Components.FirstOrDefault(component => component.Edits.Contains(name, StringComparer.Ordinal)) is { Type: { } registered }
            && typeof(ConditionEditorBase).IsAssignableFrom(registered))
        {
            return registered;
        }

        return name is not null && s_builtIn.TryGetValue(name, out var builtIn) ? builtIn : typeof(RawConditionEditor);
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Rules is null)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-rule-editor__list");
        for (var index = 0; index < Items.Count; index++)
        {
            if (Items[index] is not JsonObject condition)
            {
                continue;
            }

            var at = index;
            var name = RuleEditorContext.Name(condition);
            var type = Rules.Type(name);
            var id = RuleEditorContext.Id(condition) ?? $"#{index}";

            builder.OpenElement(10, "div");
            builder.SetKey(condition);
            builder.AddAttribute(11, "class", "grove-rule-editor__condition rz-border-base-300 rz-border-radius-2 rz-p-2 rz-mb-2");
            builder.AddAttribute(12, "style", "border: 1px solid var(--rz-base-300, #ddd);");
            builder.AddAttribute(13, "data-condition", name);

            builder.OpenElement(14, "div");
            builder.AddAttribute(15, "style", "display: flex; align-items: center; gap: 0.5rem;");
            builder.OpenElement(16, "strong");
            builder.AddContent(17, T[type?.DisplayName ?? name ?? "Condition"]);
            builder.CloseElement();
            if (!string.IsNullOrEmpty(type?.Description))
            {
                builder.OpenElement(18, "small");
                builder.AddAttribute(19, "class", "rz-color-base-600");
                builder.AddContent(20, T[type.Description]);
                builder.CloseElement();
            }

            builder.OpenElement(21, "span");
            builder.AddAttribute(22, "style", "margin-inline-start: auto;");
            Action(builder, 23, "arrow_upward", T["Move up"], Rules.Disabled || at == 0, () => Move(at, -1));
            Action(builder, 24, "arrow_downward", T["Move down"], Rules.Disabled || at == Items.Count - 1, () => Move(at, 1));
            Action(builder, 25, "delete", T["Remove"], Rules.Disabled, () => Remove(at));
            builder.CloseElement();
            builder.CloseElement();

            builder.OpenComponent(30, EditorFor(name));
            builder.AddComponentParameter(31, nameof(ConditionEditorBase.Condition), condition);
            builder.CloseComponent();

            if (Rules.Errors.TryGetValue(id, out var error))
            {
                builder.OpenElement(32, "div");
                builder.AddAttribute(33, "class", "grove-editor__error rz-color-danger");
                builder.AddContent(34, error);
                builder.CloseElement();
            }

            builder.CloseElement();
        }

        builder.OpenElement(40, "div");
        builder.AddAttribute(41, "style", "display: flex; gap: 0.5rem; align-items: center;");
        builder.OpenComponent<GroveDropDown<string>>(42);
        builder.AddComponentParameter(43, "Data", Rules.Catalog.Conditions
            .OrderBy(type => type.Category, StringComparer.Ordinal)
            .ThenBy(type => type.DisplayName, StringComparer.Ordinal)
            .Select(type => new Forms.SelectOption(T[type.DisplayName], type.Name))
            .ToArray());
        builder.AddComponentParameter(44, "TextProperty", nameof(Forms.SelectOption.Text));
        builder.AddComponentParameter(45, "ValueProperty", nameof(Forms.SelectOption.Value));
        builder.AddComponentParameter(46, "Value", _adding);
        builder.AddComponentParameter(47, "ValueChanged", EventCallback.Factory.Create<string>(this, value => _adding = value));
        builder.AddComponentParameter(48, "Placeholder", T["Condition type"].ToString());
        builder.AddComponentParameter(49, "Disabled", Rules.Disabled);
        builder.AddComponentParameter(50, "Style", "min-width: 14rem;");
        builder.AddAttribute(51, "data-testid", "rule-editor-type");
        builder.CloseComponent();
        builder.OpenComponent<GroveButton>(52);
        builder.AddComponentParameter(53, nameof(GroveButton.Text), T["Add condition"].ToString());
        builder.AddComponentParameter(54, nameof(GroveButton.Icon), "add");
        builder.AddComponentParameter(55, nameof(GroveButton.ButtonStyle), ButtonStyle.Light);
        builder.AddComponentParameter(56, nameof(GroveButton.Size), ButtonSize.Small);
        builder.AddComponentParameter(57, nameof(GroveButton.Disabled), Rules.Disabled || string.IsNullOrEmpty(_adding));
        builder.AddComponentParameter(58, nameof(GroveButton.Click), EventCallback.Factory.Create<MouseEventArgs>(this, Add));
        builder.AddAttribute(59, "data-testid", "rule-editor-add");
        builder.CloseComponent();
        builder.CloseElement();

        builder.CloseElement();
    }

    private void Action(RenderTreeBuilder builder, int sequence, string icon, string title, bool disabled, Action click)
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

    private void Add()
    {
        if (Rules is null || string.IsNullOrEmpty(_adding))
        {
            return;
        }

        Items.Add(Rules.Create(_adding));
        _adding = null;
        Rules.NotifyChanged();
    }

    private void Move(int index, int offset)
    {
        var target = index + offset;
        if (Rules is null || target < 0 || target >= Items.Count)
        {
            return;
        }

        var node = Items[index];
        Items.RemoveAt(index);
        Items.Insert(target, node);
        Rules.NotifyChanged();
    }

    private void Remove(int index)
    {
        Items.RemoveAt(index);
        Rules?.NotifyChanged();
    }
}

/// <summary>
/// A condition type's editor (Grove.Display.Rules): it edits one condition of the cascaded
/// <see cref="RuleEditorContext"/> in place, and declares the condition types it edits with
/// <c>Edits</c> (the conditions' <c>Name</c>s), as a field editor declares its field types.
/// </summary>
public abstract class ConditionEditorBase : ComponentBase, IDisposable
{
    private RuleEditorContext? _subscribed;

    [Parameter]
    [ComponentParameter(Hidden = true)]
    public JsonObject? Condition { get; set; }

    [CascadingParameter]
    public RuleEditorContext? Rules { get; set; }

    [Inject]
    protected Theme.GroveApiLocalizer T { get; set; } = null!;

    protected bool Disabled => Rules?.Disabled ?? true;

    protected RuleConditionTypeModel? ConditionType => Rules?.Type(RuleEditorContext.Name(Condition));

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_subscribed, Rules))
        {
            if (_subscribed is not null)
            {
                _subscribed.Changed -= OnChanged;
            }

            _subscribed = Rules;
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

    protected void Set(string property, JsonNode? value)
    {
        if (Condition is null)
        {
            return;
        }

        Condition[property] = value;
        Rules?.NotifyChanged();
    }

    protected string? Text(string property) =>
        Condition?[property] is JsonValue value && value.TryGetValue<string>(out var text) ? text : Condition?[property]?.ToString();

    protected bool Flag(string property, bool fallback = false) =>
        Condition?[property] is JsonValue value && value.TryGetValue<bool>(out var flag) ? flag : fallback;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Condition is null || Rules is null)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-rule-editor__fields");
        RenderInput(builder);
        builder.CloseElement();
    }

    protected abstract void RenderInput(RenderTreeBuilder builder);

    protected static void Labelled(RenderTreeBuilder builder, int sequence, string label, RenderFragment input)
    {
        builder.OpenComponent<GroveFormField>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(GroveFormField.Text), label);
        builder.AddComponentParameter(sequence + 2, nameof(GroveFormField.Style), "width: 100%;");
        builder.AddComponentParameter(sequence + 3, nameof(GroveFormField.ChildContent), input);
        builder.CloseComponent();
    }

    protected void Switch(RenderTreeBuilder builder, int sequence, string property, bool fallback)
    {
        builder.OpenComponent<GroveSwitch>(sequence);
        builder.AddComponentParameter(sequence + 1, nameof(GroveSwitch.Value), Flag(property, fallback));
        builder.AddComponentParameter(sequence + 2, nameof(GroveSwitch.ValueChanged), EventCallback.Factory.Create<bool>(this, value => Set(property, JsonValue.Create(value))));
        builder.AddComponentParameter(sequence + 3, nameof(GroveSwitch.Disabled), Disabled);
        builder.CloseComponent();
    }
}

/// <summary>An <c>All</c> or <c>Any</c> group: its name and its own list of conditions.</summary>
[BlazorComponent("ConditionGroupEditor", DisplayName = "Condition group", Category = "Editors", Icon = "account_tree", Edits = ["AllConditionGroup", "AnyConditionGroup"])]
public sealed class ConditionGroupEditor : ConditionEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        Labelled(builder, 0, T["Name"], child =>
        {
            child.OpenComponent<GroveTextBox>(0);
            child.AddComponentParameter(1, nameof(GroveTextBox.Value), Text("DisplayText"));
            child.AddComponentParameter(2, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, value => Set("DisplayText", JsonValue.Create(value))));
            child.AddComponentParameter(3, nameof(GroveTextBox.Disabled), Disabled);
            child.AddComponentParameter(4, nameof(GroveTextBox.Style), "width: 100%;");
            child.CloseComponent();
        });

        if (Condition!["Conditions"] is not JsonArray children)
        {
            Condition["Conditions"] = children = [];
        }

        builder.OpenElement(10, "div");
        builder.AddAttribute(11, "style", "padding-inline-start: 1rem; border-inline-start: 2px solid var(--rz-base-300, #ddd);");
        builder.OpenComponent<ConditionList>(12);
        builder.AddComponentParameter(13, nameof(ConditionList.Items), children);
        builder.CloseComponent();
        builder.CloseElement();
    }
}

/// <summary>A condition that is true or false as set (<c>Boolean</c>), or whose value says whether the page must be the homepage.</summary>
[BlazorComponent("BooleanConditionEditor", DisplayName = "True or false", Category = "Editors", Icon = "toggle_on", Edits = ["BooleanCondition", "HomepageCondition"])]
public sealed class BooleanConditionEditor : ConditionEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder) =>
        Labelled(builder, 0, T["Value"], child => Switch(child, 0, "Value", fallback: true));
}

/// <summary>A condition with nothing to set (is authenticated, is anonymous).</summary>
[BlazorComponent("FlagConditionEditor", DisplayName = "Condition without values", Category = "Editors", Icon = "flag", Edits = ["IsAuthenticatedCondition", "IsAnonymousCondition"])]
public sealed class FlagConditionEditor : ConditionEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
    }
}

/// <summary>
/// A condition comparing a request value with a string (URL, culture, role, content type): the
/// operator (<see cref="StringOperationPicker"/>), whether the comparison is case sensitive, and
/// the value; the values the site knows (its cultures, roles, content types) are offered.
/// </summary>
[BlazorComponent("StringConditionEditor", DisplayName = "String comparison", Category = "Editors", Icon = "compare_arrows", Edits = ["UrlCondition", "CultureCondition", "RoleCondition", "ContentTypeCondition"])]
public sealed class StringConditionEditor : ConditionEditorBase
{
    private readonly string _listId = "grove-rule-choices-" + Guid.NewGuid().ToString("n");

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        var operation = Condition!["Operation"] as JsonObject;

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "style", "display: flex; gap: 0.75rem; flex-wrap: wrap; align-items: flex-end;");

        Labelled(builder, 2, T["Operation"], child =>
        {
            child.OpenComponent<StringOperationPicker>(0);
            child.AddComponentParameter(1, nameof(StringOperationPicker.Operation), operation);
            child.AddComponentParameter(2, nameof(StringOperationPicker.OperationChanged), EventCallback.Factory.Create<JsonObject?>(this, value => Set("Operation", value)));
            child.AddComponentParameter(3, nameof(StringOperationPicker.Disabled), Disabled);
            child.CloseComponent();
        });

        Labelled(builder, 10, T["Value"], child =>
        {
            child.OpenComponent<GroveTextBox>(0);
            child.AddComponentParameter(1, nameof(GroveTextBox.Value), Text("Value"));
            child.AddComponentParameter(2, nameof(GroveTextBox.Change), EventCallback.Factory.Create<string>(this, value => Set("Value", JsonValue.Create(value))));
            child.AddComponentParameter(3, nameof(GroveTextBox.Disabled), Disabled);
            child.AddComponentParameter(4, nameof(GroveTextBox.Style), "width: 100%;");
            if (ConditionType?.Choices is { Length: > 0 })
            {
                child.AddAttribute(5, "list", _listId);
            }

            child.CloseComponent();
        });

        if (operation is not null)
        {
            Labelled(builder, 20, T["Case sensitive"], child =>
            {
                child.OpenComponent<GroveSwitch>(0);
                child.AddComponentParameter(1, nameof(GroveSwitch.Value), operation["CaseSensitive"] is JsonValue sensitive && sensitive.TryGetValue<bool>(out var on) && on);
                child.AddComponentParameter(2, nameof(GroveSwitch.ValueChanged), EventCallback.Factory.Create<bool>(this, value =>
                {
                    operation["CaseSensitive"] = value;
                    Rules?.NotifyChanged();
                }));
                child.AddComponentParameter(3, nameof(GroveSwitch.Disabled), Disabled);
                child.CloseComponent();
            });
        }

        builder.CloseElement();

        if (ConditionType?.Choices is { Length: > 0 } choices)
        {
            builder.OpenElement(30, "datalist");
            builder.AddAttribute(31, "id", _listId);
            foreach (var choice in choices)
            {
                builder.OpenElement(32, "option");
                builder.AddAttribute(33, "value", choice);
                builder.CloseElement();
            }

            builder.CloseElement();
        }
    }
}

/// <summary>A script that returns true or false (<c>Javascript</c>), checked on the server as you leave the editor.</summary>
[BlazorComponent("JavascriptConditionEditor", DisplayName = "JavaScript condition", Category = "Editors", Icon = "javascript", Edits = ["JavascriptCondition"])]
public sealed class JavascriptConditionEditor : ConditionEditorBase
{
    protected override void RenderInput(RenderTreeBuilder builder)
    {
        Labelled(builder, 0, T["Script"], child =>
        {
            child.OpenComponent<CodeEditor>(0);
            child.AddComponentParameter(1, nameof(CodeEditor.Value), Text("Script"));
            child.AddComponentParameter(2, nameof(CodeEditor.Changed), EventCallback.Factory.Create<string?>(this, value => Set("Script", JsonValue.Create(value))));
            child.AddComponentParameter(3, nameof(CodeEditor.Language), "javascript");
            child.AddComponentParameter(4, nameof(CodeEditor.Disabled), Disabled);
            child.AddComponentParameter(5, nameof(CodeEditor.Height), "8rem");
            child.CloseComponent();
        });

        builder.OpenElement(10, "small");
        builder.AddAttribute(11, "class", "rz-color-base-600");
        builder.AddContent(12, T["The script must return true or false."]);
        builder.CloseElement();
    }
}

/// <summary>A condition no registered editor edits: its JSON, as stored.</summary>
public sealed class RawConditionEditor : ConditionEditorBase
{
    private static readonly JsonSerializerOptions s_indented = new() { WriteIndented = true };

    protected override void RenderInput(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CodeEditor>(0);
        builder.AddComponentParameter(1, nameof(CodeEditor.Value), Condition!.ToJsonString(s_indented));
        builder.AddComponentParameter(2, nameof(CodeEditor.Changed), EventCallback.Factory.Create<string?>(this, Replace));
        builder.AddComponentParameter(3, nameof(CodeEditor.Language), "json");
        builder.AddComponentParameter(4, nameof(CodeEditor.Disabled), Disabled);
        builder.AddComponentParameter(5, nameof(CodeEditor.Height), "8rem");
        builder.CloseComponent();
    }

    private void Replace(string? text)
    {
        try
        {
            if (JsonNode.Parse(text ?? "{}") is JsonObject parsed && Condition is not null)
            {
                foreach (var key in Condition.Select(pair => pair.Key).ToArray())
                {
                    Condition.Remove(key);
                }

                foreach (var (key, value) in parsed.ToArray())
                {
                    parsed.Remove(key);
                    Condition[key] = value;
                }

                Rules?.NotifyChanged();
            }
        }
        catch (JsonException)
        {
        }
    }
}

/// <summary>
/// The operator a string condition compares with (equals, starts with, contains and their
/// negations), from the operators the server registers; the case sensitivity is kept across a
/// change of operator.
/// </summary>
[BlazorComponent("StringOperationPicker", DisplayName = "String operator", Category = "Editors", Icon = "compare_arrows")]
public sealed class StringOperationPicker : ComponentBase
{
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public JsonObject? Operation { get; set; }

    [Parameter]
    [ComponentOutput(DisplayName = "Changed")]
    public EventCallback<JsonObject?> OperationChanged { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    [CascadingParameter]
    public RuleEditorContext? Rules { get; set; }

    [Inject]
    private Theme.GroveApiLocalizer T { get; set; } = null!;

    private static string? Discriminator(JsonObject? node) =>
        node?["$type"] is JsonValue type && type.TryGetValue<string>(out var text) ? text : null;

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var operators = Rules?.Catalog.Operators ?? [];
        var selected = operators.FirstOrDefault(option => Discriminator(option.Template) is { } type && type == Discriminator(Operation))?.Name;

        builder.OpenComponent<GroveDropDown<string>>(0);
        builder.AddComponentParameter(1, "Data", operators.Select(option => new Forms.SelectOption(T[option.DisplayName], option.Name)).ToArray());
        builder.AddComponentParameter(2, "TextProperty", nameof(Forms.SelectOption.Text));
        builder.AddComponentParameter(3, "ValueProperty", nameof(Forms.SelectOption.Value));
        builder.AddComponentParameter(4, "Value", selected);
        builder.AddComponentParameter(5, "ValueChanged", EventCallback.Factory.Create<string>(this, ChangeAsync));
        builder.AddComponentParameter(6, "Disabled", Disabled);
        builder.AddComponentParameter(7, "Placeholder", T["Select the condition operator."].ToString());
        builder.AddComponentParameter(8, "Style", "min-width: 12rem;");
        builder.CloseComponent();
    }

    private async Task ChangeAsync(string? name)
    {
        var option = Rules?.Catalog.Operators.FirstOrDefault(candidate => candidate.Name == name);
        var operation = option?.Template.DeepClone() as JsonObject;
        if (operation is not null && Operation?["CaseSensitive"] is JsonValue sensitive)
        {
            operation["CaseSensitive"] = sensitive.DeepClone();
        }

        await OperationChanged.InvokeAsync(operation);
    }
}
