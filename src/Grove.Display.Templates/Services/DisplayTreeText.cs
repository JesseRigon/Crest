using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Grove.Display.Templates.Services;

/// <summary>
/// The text an item's page body (<c>DisplayTreeFieldSet</c>) holds, for search and for its body
/// aspect: every string a node's properties carry, through the zones; a node naming a composite
/// template adds the composite's source with its Liquid tags taken out (a migrated Liquid body is
/// a composite of its own).
/// </summary>
public static partial class DisplayTreeText
{
    public static string Extract(JsonNode? tree, IReadOnlyDictionary<string, string>? compositeSources = null)
    {
        var text = new StringBuilder();
        Walk(tree, text, compositeSources, depth: 0);
        return text.ToString().Trim();
    }

    private static void Walk(JsonNode? node, StringBuilder text, IReadOnlyDictionary<string, string>? composites, int depth)
    {
        if (node is not JsonObject json || depth > 64)
        {
            return;
        }

        if (Member(json, "component") is JsonValue component && component.TryGetValue<string>(out var name)
            && composites is not null && composites.TryGetValue(name, out var source))
        {
            Append(text, LiquidTags().Replace(source, " "));
        }

        Strings(Member(json, "properties"), text);

        if (Member(json, "zones") is JsonObject zones)
        {
            foreach (var (_, children) in zones)
            {
                foreach (var child in children as JsonArray ?? [])
                {
                    Walk(child, text, composites, depth + 1);
                }
            }
        }
    }

    private static void Strings(JsonNode? value, StringBuilder text)
    {
        switch (value)
        {
            case JsonValue scalar when scalar.TryGetValue<string>(out var content):
                Append(text, content);
                break;
            case JsonObject json:
                foreach (var (_, child) in json)
                {
                    Strings(child, text);
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    Strings(child, text);
                }

                break;
        }
    }

    private static void Append(StringBuilder text, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return;
        }

        if (text.Length > 0)
        {
            text.Append(' ');
        }

        text.Append(content.Trim());
    }

    private static JsonNode? Member(JsonObject node, string name) =>
        node.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;

    [GeneratedRegex(@"\{%.*?%\}|\{\{.*?\}\}", RegexOptions.Singleline)]
    private static partial Regex LiquidTags();
}
