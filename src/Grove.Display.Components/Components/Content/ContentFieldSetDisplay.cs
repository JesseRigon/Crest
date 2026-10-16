using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Display.Components.Content;

/// <summary>
/// A part a type defines with fields of its own and no driver (the <c>ContentFieldSet</c> shape and
/// its display types, <c>ContentFieldSet_Summary</c> by the registry's variant rule): its fields, in
/// the part's <c>Content</c> zone, grouped under the part's name. A template for
/// <c>ContentFieldSet__{FieldSetName}</c> replaces it per part.
/// </summary>
[BlazorComponent("ContentFieldSet", DisplayName = "Field group", Category = "Content", Icon = "view_agenda")]
public sealed class ContentFieldSetDisplay : ComponentBase
{
    [Parameter, ComponentZone]
    public RenderFragment? Content { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (Content is null)
        {
            return;
        }

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-content-part");
        builder.AddContent(2, Content);
        builder.CloseElement();
    }
}
