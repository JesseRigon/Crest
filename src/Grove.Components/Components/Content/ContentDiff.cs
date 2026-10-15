using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.DisplayManagement.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Grove.Components.Content;

/// <summary>
/// Two versions of an item side by side (docs/MVC/parity.md › New components): every value that
/// differs, by its path in the content (<c>TitlePart.Title</c>, <c>BagPart.ContentItems[0]...</c>),
/// with the value before and after, added and removed values marked. The bookkeeping values that
/// change with every version (ids, dates) are left out.
/// </summary>
[BlazorComponent("ContentDiff", DisplayName = "Content changes", Category = "Content", Icon = "difference")]
public sealed class ContentDiff : ComponentBase
{
    private static readonly HashSet<string> s_ignored = new(StringComparer.Ordinal)
    {
        "ContentItemVersionId", "ModifiedUtc", "PublishedUtc", "CreatedUtc", "Latest", "Published", "Id",
    };

    [Parameter]
    public JsonNode? Before { get; set; }

    [Parameter]
    public JsonNode? After { get; set; }

    [Parameter]
    public string BeforeLabel { get; set; } = "Before";

    [Parameter]
    public string AfterLabel { get; set; } = "After";

    /// <summary>The differing values: path, before (null when added), after (null when removed).</summary>
    public static IReadOnlyList<(string Path, string? Before, string? After)> Changes(JsonNode? before, JsonNode? after)
    {
        var left = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var right = new SortedDictionary<string, string>(StringComparer.Ordinal);
        Flatten(before, string.Empty, left);
        Flatten(after, string.Empty, right);

        return [.. left.Keys.Union(right.Keys).Order(StringComparer.Ordinal)
            .Select(path => (Path: path, Before: left.GetValueOrDefault(path), After: right.GetValueOrDefault(path)))
            .Where(change => !string.Equals(change.Before, change.After, StringComparison.Ordinal))];
    }

    private static void Flatten(JsonNode? node, string path, IDictionary<string, string> into)
    {
        switch (node)
        {
            case JsonObject json:
                foreach (var (name, child) in json)
                {
                    if (path.Length == 0 && s_ignored.Contains(name))
                    {
                        continue;
                    }

                    Flatten(child, path.Length == 0 ? name : $"{path}.{name}", into);
                }

                break;
            case JsonArray array:
                for (var index = 0; index < array.Count; index++)
                {
                    Flatten(array[index], $"{path}[{index}]", into);
                }

                break;
            case JsonValue value:
                into[path] = value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : value.ToJsonString();
                break;
            case null when path.Length > 0:
                break;
        }
    }

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var changes = Changes(Before, After);
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "grove-content-diff");
        builder.AddAttribute(2, "data-testid", "content-diff");
        if (changes.Count == 0)
        {
            builder.OpenElement(3, "p");
            builder.AddContent(4, "No differences.");
            builder.CloseElement();
            builder.CloseElement();
            return;
        }

        builder.OpenElement(5, "table");
        builder.AddAttribute(6, "class", "grove-content-diff__table");
        builder.OpenElement(7, "thead");
        builder.OpenElement(8, "tr");
        Cell(builder, 9, "th", "Value", null);
        Cell(builder, 10, "th", BeforeLabel, null);
        Cell(builder, 11, "th", AfterLabel, null);
        builder.CloseElement();
        builder.CloseElement();
        builder.OpenElement(12, "tbody");
        foreach (var (path, before, after) in changes)
        {
            builder.OpenElement(13, "tr");
            builder.SetKey(path);
            builder.AddAttribute(14, "class", before is null ? "grove-content-diff--added" : after is null ? "grove-content-diff--removed" : "grove-content-diff--changed");
            Cell(builder, 15, "td", path, "grove-content-diff__path");
            Cell(builder, 16, "td", before, "grove-content-diff__before");
            Cell(builder, 17, "td", after, "grove-content-diff__after");
            builder.CloseElement();
        }

        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    }

    private static void Cell(RenderTreeBuilder builder, int sequence, string element, string? text, string? @class)
    {
        builder.OpenElement(sequence, element);
        if (@class is not null)
        {
            builder.AddAttribute(sequence + 100, "class", @class);
        }

        builder.AddAttribute(sequence + 101, "style", "vertical-align: top; white-space: pre-wrap; word-break: break-word; padding: 0.25rem 0.5rem;");
        builder.AddContent(sequence + 102, text ?? "—");
        builder.CloseElement();
    }
}
