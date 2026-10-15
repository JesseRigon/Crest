using System.Globalization;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Components.Content;

/// <summary>
/// A numbered pager (the <c>Pager</c> shape, docs/blazor-display.md rework step 7): links to the
/// pages around the current one as <c>?pagenum=N</c> (or the pager's own id), relative to the
/// page, so the site's content route rebuilds the page for that query.
/// </summary>
[BlazorComponent("Pager", DisplayName = "Pager", Category = "Navigation", Icon = "last_page")]
public sealed class Pager : ComponentBase
{
    [Parameter]
    public int Page { get; set; } = 1;

    [Parameter]
    public int PageSize { get; set; } = 10;

    [Parameter]
    public int TotalItemCount { get; set; }

    /// <summary>The query key of the page number; <c>pagenum</c> when empty.</summary>
    [Parameter]
    [ComponentParameter(Hidden = true)]
    public string? PagerId { get; set; }

    /// <summary>How many page links show on each side of the current one.</summary>
    [Parameter]
    public int Quantity { get; set; } = 3;

    /// <summary>The pages a pager shows: the current one and its neighbours, within the total.</summary>
    public static IReadOnlyList<int> Pages(int page, int pageSize, int total, int quantity)
    {
        var count = pageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var current = Math.Clamp(page, 1, count);
        var first = Math.Max(1, current - quantity);
        var last = Math.Min(count, current + quantity);
        return [.. Enumerable.Range(first, last - first + 1)];
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var count = PageSize <= 0 ? 1 : (int)Math.Ceiling(TotalItemCount / (double)PageSize);
        if (count <= 1)
        {
            return;
        }

        var key = string.IsNullOrEmpty(PagerId) ? "pagenum" : PagerId;
        var current = Math.Clamp(Page, 1, count);
        builder.OpenElement(0, "nav");
        builder.AddAttribute(1, "class", "grove-pager");
        builder.AddAttribute(2, "aria-label", "Pages");
        builder.OpenElement(3, "ul");
        Link(builder, key, current - 1, "Previous", current > 1, current: false);
        foreach (var page in Pages(current, PageSize, TotalItemCount, Quantity))
        {
            Link(builder, key, page, page.ToString(CultureInfo.CurrentCulture), enabled: true, current: page == current);
        }

        Link(builder, key, current + 1, "Next", current < count, current: false);
        builder.CloseElement();
        builder.CloseElement();
    }

    private static void Link(RenderTreeBuilder builder, string key, int page, string text, bool enabled, bool current)
    {
        builder.OpenElement(10, "li");
        builder.AddAttribute(11, "class", current ? "grove-pager__page grove-pager__page--current" : "grove-pager__page");
        if (enabled && !current)
        {
            builder.OpenElement(12, "a");
            builder.AddAttribute(13, "href", "?" + Uri.EscapeDataString(key) + "=" + page.ToString(CultureInfo.InvariantCulture));
            builder.AddContent(14, text);
            builder.CloseElement();
        }
        else
        {
            builder.OpenElement(15, "span");
            if (current)
            {
                builder.AddAttribute(16, "aria-current", "page");
            }

            builder.AddContent(17, text);
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}

/// <summary>A previous/next pager by cursor (the <c>PagerSlim</c> shape): <c>?before=</c> and <c>?after=</c>.</summary>
[BlazorComponent("PagerSlim", DisplayName = "Pager (previous and next)", Category = "Navigation", Icon = "swap_horiz")]
public sealed class PagerSlim : ComponentBase
{
    [Parameter]
    public string? Before { get; set; }

    [Parameter]
    public string? After { get; set; }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (string.IsNullOrEmpty(Before) && string.IsNullOrEmpty(After))
        {
            return;
        }

        builder.OpenElement(0, "nav");
        builder.AddAttribute(1, "class", "grove-pager grove-pager--slim");
        builder.AddAttribute(2, "aria-label", "Pages");
        if (!string.IsNullOrEmpty(Before))
        {
            builder.OpenElement(3, "a");
            builder.AddAttribute(4, "href", "?before=" + Uri.EscapeDataString(Before));
            builder.AddContent(5, "Newer");
            builder.CloseElement();
        }

        if (!string.IsNullOrEmpty(After))
        {
            builder.OpenElement(6, "a");
            builder.AddAttribute(7, "href", "?after=" + Uri.EscapeDataString(After));
            builder.AddContent(8, "Older");
            builder.CloseElement();
        }

        builder.CloseElement();
    }
}
