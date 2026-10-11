using System.Text.Json;
using System.Text.Json.Nodes;

namespace Crest.Workflows.Services;

/// <summary>
/// Turns JSON (elements, nodes, or dictionaries holding them) into plain CLR values:
/// dictionaries, lists, strings, numbers and booleans. Used where a response or request body
/// becomes workflow data, so expressions and the journal see ordinary values.
/// </summary>
public static class JsonPlain
{
    public static object? ToPlain(object? value) => value switch
    {
        null => null,
        JsonElement element => ToPlain(element),
        JsonObject node => node.ToDictionary(p => p.Key, p => ToPlain(p.Value)!),
        JsonArray array => array.Select(ToPlain).ToList(),
        JsonValue jsonValue => ToPlain(jsonValue.GetValue<JsonElement>()),
        IDictionary<string, object> dictionary => dictionary.ToDictionary(p => p.Key, p => ToPlain(p.Value)!),
        _ => value,
    };

    private static object? ToPlain(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToPlain(p.Value)!),
        JsonValueKind.Array => element.EnumerateArray().Select(ToPlain).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };
}
