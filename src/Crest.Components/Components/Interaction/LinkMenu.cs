using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Crest.Components.Interaction;

/// <summary>
/// A menu of links (<c>Menu</c>, the platform's menu shape): the menu module builds the items
/// (permissions, levels, resolved URLs) and the tree carries them; this draws them as a
/// navigation landmark. Child items nest as sub-lists.
/// </summary>
[CrestBlazorComponent("Menu", DisplayName = "Menu", Category = "Navigation", Icon = "menu")]
public sealed class LinkMenu : ComponentBase
{
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string? MenuName { get; set; }

    [Parameter]
    [ComponentZone(Allowed = ["MenuItem"])]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "nav");
        builder.AddAttribute(1, "class", "crest-menu");
        builder.AddAttribute(2, "aria-label", MenuName);
        builder.OpenElement(3, "ul");
        builder.AddAttribute(4, "class", "crest-menu__list");
        builder.AddContent(5, ChildContent);
        builder.CloseElement();
        builder.CloseElement();
    }
}

/// <summary>One link of a <see cref="LinkMenu"/> (<c>MenuItem</c>), with its children as a sub-list.</summary>
[CrestBlazorComponent("MenuItem", DisplayName = "Menu item", Category = "Navigation", Icon = "link")]
public sealed class LinkMenuItem : ComponentBase
{
    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public string? Url { get; set; }

    [Parameter]
    [ComponentParameter(Choices = ["_self", "_blank"])]
    public string? Target { get; set; }

    [Parameter]
    [ComponentParameter(Hidden = true)]
    public int Level { get; set; }

    [Parameter]
    [ComponentZone(Allowed = ["MenuItem"])]
    public RenderFragment? ChildContent { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "li");
        builder.AddAttribute(1, "class", ChildContent is null ? "crest-menu__item" : "crest-menu__item crest-menu__item--parent");
        if (string.IsNullOrEmpty(Url))
        {
            builder.OpenElement(2, "span");
            builder.AddAttribute(3, "class", "crest-menu__label");
            builder.AddContent(4, Text);
            builder.CloseElement();
        }
        else
        {
            builder.OpenElement(5, "a");
            builder.AddAttribute(6, "class", "crest-menu__link");
            builder.AddAttribute(7, "href", IsSafe(Url) ? Url : "#");
            if (Target == "_blank")
            {
                builder.AddAttribute(8, "target", "_blank");
                builder.AddAttribute(9, "rel", "noopener noreferrer");
            }

            builder.AddContent(10, Text);
            builder.CloseElement();
        }

        if (ChildContent is not null)
        {
            builder.OpenElement(11, "ul");
            builder.AddAttribute(12, "class", "crest-menu__list crest-menu__list--nested");
            builder.AddContent(13, ChildContent);
            builder.CloseElement();
        }

        builder.CloseElement();
    }

    // Security audit item 6: a URL parameter never carries a script.
    private static bool IsSafe(string url) =>
        !url.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
        && !url.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase)
        && !url.TrimStart().StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase);
}
