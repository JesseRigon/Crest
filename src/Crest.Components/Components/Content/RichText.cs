using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Crest.Components.Content;

/// <summary>
/// Rich text a template places (docs/widgets.md, the Paragraph widget): HTML in a block element.
/// The parameter is a markup value, so the server sanitizes it when the tree is filtered for the
/// viewer (§ 10: markup is sanitized on save and on render); the browser renders what arrives.
/// Unsanitized HTML and scripts are not offered (the scripting ruling).
/// </summary>
[BlazorComponent("RichText", DisplayName = "Rich text", Category = "Typography", Icon = "article")]
public sealed class RichText : ComponentBase
{
    [Parameter]
    [ComponentParameter(Editor = ComponentSchema.Editors.Markup, DisplayName = "Text", Group = "Content")]
    public string? Html { get; set; }

    /// <summary>Class names from the design system (tokens), never raw CSS.</summary>
    [Parameter]
    [ComponentParameter(Advanced = true)]
    public string? Class { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", string.IsNullOrEmpty(Class) ? "crest-rich-text" : "crest-rich-text " + Class);
        builder.AddMarkupContent(2, Html ?? string.Empty);
        builder.CloseElement();
    }
}

/// <summary>
/// An image with a caption (docs/widgets.md, the Image widget): a <c>figure</c> around the image
/// and its <c>figcaption</c>. The path is a media picker's value, a placement id rendered as its
/// <c>/files</c> URL (<see cref="Files.MediaReference"/>); the caption is plain text.
/// </summary>
[BlazorComponent("Figure", DisplayName = "Figure", Category = "Basic", Icon = "image")]
public sealed class Figure : ComponentBase
{
    [Parameter]
    [ComponentParameter(Editor = ComponentSchema.Editors.Media, DisplayName = "Image", Group = "Content", Required = true)]
    public string? Path { get; set; }

    [Parameter]
    [ComponentParameter(DisplayName = "Alternate text", Group = "Content")]
    public string? AlternateText { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Caption { get; set; }

    [Parameter]
    [ComponentParameter(Advanced = true)]
    public string? Class { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "figure");
        builder.AddAttribute(1, "class", string.IsNullOrEmpty(Class) ? "crest-figure" : "crest-figure " + Class);
        if (Files.MediaReference.Url(Path) is { } source)
        {
            builder.OpenElement(2, "img");
            builder.AddAttribute(3, "src", source);
            builder.AddAttribute(4, "alt", AlternateText ?? string.Empty);
            builder.AddAttribute(5, "loading", "lazy");
            builder.CloseElement();
        }

        if (!string.IsNullOrWhiteSpace(Caption))
        {
            builder.OpenElement(6, "figcaption");
            builder.AddContent(7, Caption);
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}

/// <summary>A URL a component may emit as a link or an image source: anything but a script or
/// data scheme (security audit item 6).</summary>
public static class SafeUrl
{
    private static readonly string[] s_refused = ["javascript:", "data:", "vbscript:"];

    public static bool IsSafe(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && !s_refused.Any(scheme => url.TrimStart().StartsWith(scheme, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// The display of an HTML body (the <c>HtmlBodyPart</c> shape its driver builds): the body's
/// rendered HTML as a markup parameter, so the server sanitizes it and the page no longer falls
/// back to the Razor view for it (rework step 7).
/// </summary>
[BlazorComponent("HtmlBodyPart", DisplayName = "HTML body", Category = "Content", Icon = "article")]
public sealed class HtmlBodyDisplay : ComponentBase
{
    [Parameter]
    [ComponentParameter(Editor = ComponentSchema.Editors.Markup, Hidden = true)]
    public string? Html { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "crest-rich-text");
        builder.AddMarkupContent(2, Html ?? string.Empty);
        builder.CloseElement();
    }
}

/// <summary>The display of a Markdown body (the <c>MarkdownBodyPart</c> shape): its HTML, rendered on the server and sanitized there.</summary>
[BlazorComponent("MarkdownBodyPart", DisplayName = "Markdown body", Category = "Content", Icon = "notes")]
public sealed class MarkdownBodyDisplay : ComponentBase
{
    [Parameter]
    [ComponentParameter(Editor = ComponentSchema.Editors.Markup, Hidden = true)]
    public string? Html { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "crest-rich-text");
        builder.AddMarkupContent(2, Html ?? string.Empty);
        builder.CloseElement();
    }
}
