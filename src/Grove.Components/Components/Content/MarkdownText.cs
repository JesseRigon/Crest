using Grove.Components.Primitives;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Components.Content;

/// <summary>
/// Markdown a template places (docs/blazor-display.md, primitives): <see cref="GroveMarkdown"/>
/// with HTML pass-through off, so only Markdown syntax renders and nothing a tenant writes can
/// reach the page as markup (the reason the Radzen-derived component itself stays unregistered:
/// its tag and attribute allow-lists would be a template's to set).
/// </summary>
[BlazorComponent("Markdown", DisplayName = "Markdown", Category = "Typography", Icon = "notes")]
public sealed class MarkdownText : ComponentBase
{
    [Parameter]
    [ComponentParameter(Group = "Content", Required = true)]
    public string? Text { get; set; }

    [Parameter]
    [ComponentParameter(Advanced = true)]
    public string? Class { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenComponent<GroveMarkdown>(0);
        builder.AddComponentParameter(1, nameof(GroveMarkdown.Text), Text ?? string.Empty);
        builder.AddComponentParameter(2, nameof(GroveMarkdown.AllowHtml), false);
        if (!string.IsNullOrEmpty(Class))
        {
            builder.AddAttribute(3, "class", Class);
        }

        builder.CloseComponent();
    }
}
