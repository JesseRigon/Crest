using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Crest.SiteTheme.Client.Components;

/// <summary>
/// The site theme's layout (docs/blazor-display.md § 6, rework step 3): the page frame the
/// <c>Layout</c> shape binds to, with its zones as the well-known keys (decision 6). A tenant
/// template named <c>Layout</c> replaces it. The design-system tokens style it; no theme CSS
/// framework is loaded.
/// </summary>
[CrestBlazorComponent("Layout", DisplayName = "Site layout", Category = "Layout", Icon = "web", Instances = ComponentInstances.Single, IsHost = true)]
public sealed class SiteLayout : ComponentBase
{
    [Parameter]
    public string? SiteName { get; set; }

    /// <summary>The page's heading when no Header zone fills the masthead.</summary>
    [Parameter]
    public string? Title { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Navigation { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Header { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Messages { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Content { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Sidebar { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Footer { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "crest-site");

        builder.OpenElement(2, "header");
        builder.AddAttribute(3, "class", "crest-site__bar");
        builder.OpenElement(4, "a");
        builder.AddAttribute(5, "class", "crest-site__brand");
        builder.AddAttribute(6, "href", "./");
        builder.AddContent(7, SiteName);
        builder.CloseElement();
        builder.AddContent(8, Navigation);
        builder.CloseElement();

        builder.OpenElement(9, "section");
        builder.AddAttribute(10, "class", "crest-site__masthead");
        if (Header is not null)
        {
            builder.AddContent(11, Header);
        }
        else if (!string.IsNullOrEmpty(Title))
        {
            builder.OpenElement(12, "h1");
            builder.AddAttribute(13, "class", "crest-site__title");
            builder.AddContent(14, Title);
            builder.CloseElement();
        }

        builder.CloseElement();

        builder.OpenElement(15, "div");
        builder.AddAttribute(16, "class", Sidebar is null ? "crest-site__body" : "crest-site__body crest-site__body--aside");
        builder.OpenElement(17, "main");
        builder.AddAttribute(18, "class", "crest-site__main");
        builder.AddContent(19, Messages);
        builder.AddContent(20, Content);
        builder.CloseElement();
        if (Sidebar is not null)
        {
            builder.OpenElement(21, "aside");
            builder.AddAttribute(22, "class", "crest-site__aside");
            builder.AddContent(23, Sidebar);
            builder.CloseElement();
        }

        builder.CloseElement();

        builder.OpenElement(24, "footer");
        builder.AddAttribute(25, "class", "crest-site__footer");
        builder.AddContent(26, Footer);
        builder.CloseElement();

        builder.CloseElement();
    }
}

/// <summary>
/// A content item's frame on the site (the <c>Content</c> shape and its type alternates): its
/// parts and fields arrive in the zones placement put them in. A template for
/// <c>Content__{Type}</c> replaces it per type.
/// </summary>
[CrestBlazorComponent("Content", DisplayName = "Content item", Category = "Content", Icon = "article")]
public sealed class SiteContent : ComponentBase
{
    [Parameter, ComponentZone]
    public RenderFragment? Header { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Meta { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Content { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Tags { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Actions { get; set; }

    [Parameter, ComponentZone]
    public RenderFragment? Footer { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "article");
        builder.AddAttribute(1, "class", "crest-content");
        Section(builder, 2, "header", "crest-content__header", Header);
        Section(builder, 10, "div", "crest-content__meta", Meta);
        Section(builder, 20, "div", "crest-content__body", Content);
        Section(builder, 30, "div", "crest-content__tags", Tags);
        Section(builder, 40, "div", "crest-content__actions", Actions);
        Section(builder, 50, "footer", "crest-content__footer", Footer);
        builder.CloseElement();
    }

    private static void Section(RenderTreeBuilder builder, int sequence, string element, string cssClass, RenderFragment? fragment)
    {
        if (fragment is null)
        {
            return;
        }

        builder.OpenElement(sequence, element);
        builder.AddAttribute(sequence + 1, "class", cssClass);
        builder.AddContent(sequence + 2, fragment);
        builder.CloseElement();
    }
}

/// <summary>The site's not-found page (<c>NotFound</c>): the last fallback of the template chain (decision 7).</summary>
[CrestBlazorComponent("NotFound", DisplayName = "Not found", Category = "Pages", Icon = "search_off")]
public sealed class SiteNotFound : ComponentBase
{
    [Parameter]
    public string Message { get; set; } = "This page does not exist.";

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "crest-site__notice");
        builder.AddAttribute(2, "data-testid", "site-not-found");
        builder.AddContent(3, Message);
        builder.CloseElement();
    }
}

/// <summary>The site's forbidden page (<c>Forbidden</c>).</summary>
[CrestBlazorComponent("Forbidden", DisplayName = "Forbidden", Category = "Pages", Icon = "block")]
public sealed class SiteForbidden : ComponentBase
{
    [Parameter]
    public string Message { get; set; } = "You may not view this page.";

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "p");
        builder.AddAttribute(1, "class", "crest-site__notice");
        builder.AddAttribute(2, "data-testid", "site-forbidden");
        builder.AddContent(3, Message);
        builder.CloseElement();
    }
}
