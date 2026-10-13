using Crest.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Crest.Components.Forms;

/// <summary>
/// A search box (docs/widgets.md, the SearchForm widget): a GET form to the search page
/// (<c>search/{index}?Terms=…</c>, relative to the shell's base), so it works before the page
/// is interactive and the search page reads the query as it always has.
/// </summary>
[BlazorComponent("SearchBox", DisplayName = "Search box", Category = "Navigation", Icon = "search")]
public sealed class SearchBox : ComponentBase
{
    /// <summary>The search index; the site's default index when absent.</summary>
    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Index { get; set; }

    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string? Placeholder { get; set; }

    /// <summary>The accessible name of the box and the button's text.</summary>
    [Parameter]
    [ComponentParameter(Group = "Content")]
    public string Label { get; set; } = "Search";

    [Parameter]
    [ComponentParameter(Advanced = true)]
    public string? Class { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "form");
        builder.AddAttribute(1, "class", string.IsNullOrEmpty(Class) ? "crest-search-box" : "crest-search-box " + Class);
        builder.AddAttribute(2, "role", "search");
        builder.AddAttribute(3, "method", "get");
        builder.AddAttribute(4, "action", string.IsNullOrWhiteSpace(Index) ? "search" : "search/" + Uri.EscapeDataString(Index.Trim()));
        builder.OpenElement(5, "input");
        builder.AddAttribute(6, "type", "search");
        builder.AddAttribute(7, "name", "Terms");
        builder.AddAttribute(8, "aria-label", Label);
        builder.AddAttribute(9, "placeholder", Placeholder);
        builder.CloseElement();
        builder.OpenElement(10, "button");
        builder.AddAttribute(11, "type", "submit");
        builder.AddAttribute(12, "class", "rz-button rz-primary");
        builder.AddContent(13, Label);
        builder.CloseElement();
        builder.CloseElement();
    }
}
