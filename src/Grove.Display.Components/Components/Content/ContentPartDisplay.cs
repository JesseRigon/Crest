using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Display.Components.Content;

/// <summary>
/// A part a type defines with fields of its own and no driver (the <c>ContentPart</c> shape and
/// its display types, <c>ContentPart_Summary</c> by the registry's variant rule): its fields, in
/// the part's <c>Content</c> zone, grouped under the part's name. A template for
/// <c>ContentPart__{PartName}</c> replaces it per part.
/// </summary>
[BlazorComponent("ContentPart", DisplayName = "Content part", Category = "Content", Icon = "view_agenda")]
public sealed class ContentPartDisplay : ComponentBase
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
