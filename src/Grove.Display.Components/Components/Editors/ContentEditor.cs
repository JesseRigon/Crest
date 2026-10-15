using System.Text.Json.Nodes;
using Grove.Display.Components.Models;
using Grove.Display.Components.Primitives;
using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Display.Components.Editors;

/// <summary>
/// The root of an editor tree (docs/blazor-display.md § 7, rework step 5): it holds the item
/// being edited (its content and its type's definition) as the <see cref="ContentEditorContext"/>
/// its editor nodes cascade from, and publishes the edited content as an output, which the page
/// that owns the tree saves. The server lays the editors out (a section per part, an editor per
/// field, picked by what each editor declares it edits); a template for
/// <c>ContentEditor__{Type}</c> lays them out differently, which is a per-type designed editor.
/// </summary>
[BlazorComponent("ContentEditor", DisplayName = "Content editor", Category = "Editors", Icon = "edit_note", Instances = ComponentInstances.Single)]
public sealed class ContentEditor : ComponentBase, IDisposable
{
    [Parameter]
    public string? ContentType { get; set; }

    /// <summary>The item's id; none for an item not saved yet.</summary>
    [Parameter]
    public string? ContentItemId { get; set; }

    /// <summary>The item's content JSON, read once when the editor opens.</summary>
    [Parameter]
    public JsonNode? Content { get; set; }

    /// <summary>The type's definition as the content types API serves it.</summary>
    [Parameter]
    public JsonNode? Definition { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Filled by the renderer: the save's errors (by editor path) are kept under it in the display state.</summary>
    [Parameter]
    [ComponentParameter(Hidden = true, ReadOnly = true)]
    public string? InstanceId { get; set; }

    /// <summary>The tree's state: the page that saves puts the server's errors under this node.</summary>
    [CascadingParameter]
    public DisplayState? State { get; set; }

    private DisplayState? _subscribed;

    /// <summary>The content as edited, on every change.</summary>
    [Parameter]
    [ComponentOutput(DisplayName = "Content")]
    public EventCallback<JsonNode?> ContentChanged { get; set; }

    [Parameter]
    [ComponentZone]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    /// <summary>The editing context, for the page that owns the tree (validation before a save).</summary>
    public ContentEditorContext? Context { get; private set; }

    protected override void OnParametersSet()
    {
        // The content is read once: later renders hand in the same JSON again, and the edits live
        // in the context, not in the tree.
        if (Context is null)
        {
            Context = new ContentEditorContext(Content?.DeepClone(), GroveContentTypeMetadata.FromTypeDefinition(Definition), Services.GetService<HttpClient>())
            {
                ContentItemId = string.IsNullOrEmpty(ContentItemId) ? null : ContentItemId,
            };
            Context.Changed += OnChanged;
        }

        Context.Disabled = Disabled;

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
    }

    // The server's errors of the last save, keyed as the editors are (Part.Field, Part), shown
    // under each editor.
    private void OnStateChanged()
    {
        if (Context is null || State is null || InstanceId is null)
        {
            return;
        }

        // Only a new set of errors changes the editors (the content's own changes also raise the state's event).
        var errors = State.GetErrors(InstanceId);
        if (ReferenceEquals(errors, _lastErrors) || (errors.Count == 0 && _shown.Count == 0))
        {
            return;
        }

        _lastErrors = errors;

        foreach (var key in _shown)
        {
            Context.Errors.Remove(key);
        }

        _shown.Clear();
        foreach (var (key, messages) in errors)
        {
            if (key.Length > 0)
            {
                Context.Errors[key] = string.Join(" ", messages);
                _shown.Add(key);
            }
        }

        Context.Refresh();
    }

    private readonly List<string> _shown = [];
    private IReadOnlyDictionary<string, IReadOnlyList<string>>? _lastErrors;

    private void OnChanged() => _ = InvokeAsync(() => ContentChanged.InvokeAsync(Context!.Content.DeepClone()));

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<CascadingValue<ContentEditorContext>>(0);
        builder.AddComponentParameter(1, "Value", Context);
        builder.AddComponentParameter(2, "IsFixed", true);
        builder.AddComponentParameter(3, "ChildContent", (RenderFragment)(child =>
        {
            child.OpenElement(4, "div");
            child.AddAttribute(5, "class", "grove-content-editor");
            child.AddAttribute(6, "data-testid", "content-editor");
            child.AddContent(7, ChildContent);
            child.CloseElement();
        }));
        builder.CloseComponent();
    }

    public void Dispose()
    {
        if (_subscribed is not null)
        {
            _subscribed.Changed -= OnStateChanged;
        }

        if (Context is not null)
        {
            Context.Changed -= OnChanged;
        }
    }
}

/// <summary>A titled group of editors (a part's fields).</summary>
[BlazorComponent("EditorSection", DisplayName = "Editor section", Category = "Editors", Icon = "segment")]
public sealed class EditorSection : ComponentBase
{
    [Parameter]
    public string? Title { get; set; }

    [Parameter]
    [ComponentZone]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<GroveFieldset>(0);
        builder.AddComponentParameter(1, nameof(GroveFieldset.Text), Title);
        builder.AddComponentParameter(2, nameof(GroveFieldset.AllowCollapse), true);
        builder.AddComponentParameter(3, nameof(GroveFieldset.ChildContent), (RenderFragment)(child =>
        {
            child.OpenElement(4, "div");
            child.AddAttribute(5, "class", "grove-editor-section");
            child.AddContent(6, ChildContent);
            child.CloseElement();
        }));
        builder.CloseComponent();
    }
}
