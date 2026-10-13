using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Crest.Components.Interaction;

/// <summary>Renders its children and nothing else (<see cref="DisplayNode.FragmentComponent"/>):
/// a template with several top-level nodes, a zone's contents moved as one.</summary>
[BlazorComponent(DisplayNode.FragmentComponent, DisplayName = "Fragment", Category = "Layout", Icon = "data_array")]
public sealed class Fragment : ComponentBase
{
    [Parameter]
    [ComponentZone]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, ChildContent);
}

/// <summary>A block element around its children, styled by tokens: the base set's layout container.</summary>
[BlazorComponent("Container", DisplayName = "Container", Category = "Layout", Icon = "check_box_outline_blank")]
public sealed class Container : ComponentBase
{
    /// <summary>The element: a landmark (<c>header</c>, <c>nav</c>, <c>main</c>, <c>aside</c>, <c>footer</c>, <c>section</c>) or <c>div</c>.</summary>
    [Parameter]
    [ComponentParameter(Choices = ["div", "section", "header", "nav", "main", "aside", "footer", "article"])]
    public string Element { get; set; } = "div";

    /// <summary>Class names from the design system (tokens), never raw CSS.</summary>
    [Parameter]
    public string? Class { get; set; }

    [Parameter]
    [ComponentZone]
    public RenderFragment? ChildContent { get; set; }

    private static readonly HashSet<string> s_elements = new(StringComparer.OrdinalIgnoreCase) { "div", "section", "header", "nav", "main", "aside", "footer", "article" };

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, s_elements.Contains(Element) ? Element.ToLowerInvariant() : "div");
        builder.AddAttribute(1, "class", Class);
        builder.AddContent(2, ChildContent);
        builder.CloseElement();
    }
}
