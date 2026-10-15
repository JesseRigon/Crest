using System.Text.Json.Nodes;
using Grove.Components.Models;
using Grove.Components.Primitives;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Components.Editors;

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

    /// <summary>The item's content JSON, read once when the editor opens.</summary>
    [Parameter]
    public JsonNode? Content { get; set; }

    /// <summary>The type's definition as the content types API serves it.</summary>
    [Parameter]
    public JsonNode? Definition { get; set; }

    [Parameter]
    public bool Disabled { get; set; }

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
            Context = new ContentEditorContext(Content?.DeepClone(), GroveContentTypeMetadata.FromTypeDefinition(Definition), Services.GetService<HttpClient>());
            Context.Changed += OnChanged;
        }

        Context.Disabled = Disabled;
    }

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
