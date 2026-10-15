using Grove.Display.Management.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Grove.Display.Components.Content;

/// <summary>
/// A <c>&lt;link&gt;</c> contributed to the page head (docs/MVC/parity.md › New components): a
/// list's RSS feed, an alternate version of the page. Renders nothing in the body.
/// </summary>
[BlazorComponent("HeadLink", DisplayName = "Head link", Category = "Document", Icon = "link")]
public class HeadLink : ComponentBase
{
    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Rel { get; set; }

    /// <summary>The linked resource's media type (<c>application/rss+xml</c>).</summary>
    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Type { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Href { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Title { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (string.IsNullOrWhiteSpace(Href) || string.IsNullOrWhiteSpace(Rel))
        {
            return;
        }

        builder.OpenComponent<HeadContent>(0);
        builder.AddComponentParameter(1, nameof(HeadContent.ChildContent), (RenderFragment)(head =>
        {
            head.OpenElement(2, "link");
            head.AddAttribute(3, "rel", Rel);
            head.AddAttribute(4, "type", Type);
            head.AddAttribute(5, "href", Href);
            head.AddAttribute(6, "title", Title);
            head.CloseElement();
        }));
        builder.CloseComponent();
    }
}

/// <summary>A list's RSS link (the <c>ListPartFeed</c> shape: its <c>Rel</c>, <c>Type</c>, <c>Href</c> and <c>Title</c>).</summary>
[BlazorComponent("ListPartFeed", DisplayName = "List feed link", Category = "Document", Icon = "rss_feed")]
public sealed class ListPartFeedLink : HeadLink;
