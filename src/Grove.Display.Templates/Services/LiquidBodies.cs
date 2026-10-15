using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Grove.Display.Templates.Services;

/// <summary>
/// The conversion of a retired <c>LiquidPart</c> body into a <c>DisplayTreePart</c> tree (docs/MVC/shape-engine.md
/// › Component coverage): one body format. A body that only renders a shape by name becomes that
/// component's node; a body without Liquid becomes a <c>RichText</c> node (sanitized markup, as every
/// stored markup leaf is); a body with Liquid becomes a composite template of its own, placed by name,
/// whose free text the template engine turns into sanitized markup leaves. The item it belongs to is
/// the composite's <c>ContentItemId</c> input: <c>Model.ContentItem</c> in the body reads the item
/// through the <c>Content</c> accessor.
/// </summary>
public static partial class LiquidBodies
{
    /// <summary>What a body becomes: the tree to store, and the composite to create when it holds Liquid.</summary>
    public sealed record Conversion(JsonObject Tree, string? CompositeName, string? CompositeSource);

    public static Conversion Convert(string? liquid, string contentItemId)
    {
        var body = liquid?.Trim() ?? string.Empty;
        if (ShapeRender().Match(body) is { Success: true } match)
        {
            return new Conversion(new JsonObject { ["component"] = match.Groups["name"].Value }, null, null);
        }

        if (!body.Contains("{{", StringComparison.Ordinal) && !body.Contains("{%", StringComparison.Ordinal))
        {
            return new Conversion(RichText(body), null, null);
        }

        var name = CompositeName(contentItemId);
        var source = body.Replace("Model.ContentItem", "Content.ContentItemId[Model.ContentItemId]", StringComparison.Ordinal);
        var tree = new JsonObject
        {
            ["component"] = name,
            ["properties"] = new JsonObject { ["ContentItemId"] = contentItemId },
        };

        return new Conversion(tree, name, source);
    }

    /// <summary>The composite a body with Liquid becomes: one per item, named after it.</summary>
    public static string CompositeName(string contentItemId) => "LiquidBody_" + contentItemId;

    private static JsonObject RichText(string html) => new()
    {
        ["component"] = "RichText",
        ["properties"] = new JsonObject { ["Html"] = html },
    };

    [GeneratedRegex("""^\{\{\s*"(?<name>[A-Za-z_][A-Za-z0-9_]*)"\s*\|\s*shape_new\s*\|\s*shape_render\s*\}\}$""")]
    private static partial Regex ShapeRender();
}
