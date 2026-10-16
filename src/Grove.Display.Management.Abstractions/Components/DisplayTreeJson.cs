using System.Text.Json;
using System.Text.Json.Nodes;

namespace Grove.Display.Management.Components;

/// <summary>
/// A display tree authored as data (docs/widgets.md, the <c>DisplayTreeFieldSet</c>): the same JSON
/// the server sends, read back from an item. A node without an id gets one from its position,
/// so an author writes <c>{ "component": "RichText", "properties": { "Html": "..." } }</c>; the
/// ids are what bindings and overrides refer to, so an author who needs them writes them.
/// </summary>
public static class DisplayTreeJson
{
    public static bool TryParse(JsonNode? stored, out DisplayNode? root, out string? error)
    {
        root = null;
        error = null;
        if (stored is not JsonObject json)
        {
            error = "The tree is a JSON object with a component.";
            return false;
        }

        try
        {
            var counter = 0;
            var filled = (JsonObject)json.DeepClone();
            FillIds(filled, ref counter);
            root = filled.Deserialize<DisplayNode>(ComponentParameters.JsonOptions);
            if (root is null)
            {
                error = "The tree is empty.";
                return false;
            }

            return true;
        }
        catch (JsonException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static void FillIds(JsonObject node, ref int counter)
    {
        if (Member(node, "id") is not JsonValue id || !id.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
        {
            node["id"] = "n" + (++counter);
        }

        if (Member(node, "zones") is not JsonObject zones)
        {
            return;
        }

        foreach (var (_, children) in zones)
        {
            foreach (var child in (children as JsonArray ?? []).OfType<JsonObject>())
            {
                FillIds(child, ref counter);
            }
        }
    }

    private static JsonNode? Member(JsonObject node, string name) =>
        node.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
}
