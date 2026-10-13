#nullable enable

using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;

namespace Crest.Contents.AuditTrail.Services;

/// <summary>
/// The diff a content event stores instead of a snapshot (docs/audit.md › Content history is
/// stored as diffs). Objects are compared key by key; any other value, arrays included, is
/// replaced whole when it differs. An operation is <c>{"op":"set","path":[...],"value":...}</c>
/// or <c>{"op":"remove","path":[...]}</c>, the path being the object keys from the root.
/// </summary>
public static class JsonTreeDiff
{
    public const string Set = "set";
    public const string Remove = "remove";

    public static JsonArray Diff(JsonNode? before, JsonNode? after)
    {
        var operations = new JsonArray();
        Walk(before, after, [], operations);
        return operations;
    }

    /// <summary>Applies <paramref name="operations"/> to a copy of <paramref name="document"/>.</summary>
    public static JsonNode? Apply(JsonNode? document, JsonArray? operations)
    {
        var result = document?.DeepClone();
        if (operations is null)
        {
            return result;
        }

        foreach (var operation in operations.OfType<JsonObject>())
        {
            var path = (operation["path"] as JsonArray)?.Select(segment => segment!.GetValue<string>()).ToArray() ?? [];
            var op = operation["op"]?.GetValue<string>();

            if (path.Length == 0)
            {
                result = op == Remove ? null : operation["value"]?.DeepClone();
                continue;
            }

            result ??= new JsonObject();
            if (result is not JsonObject root)
            {
                result = root = new JsonObject();
            }

            var parent = root;
            for (var index = 0; index < path.Length - 1; index++)
            {
                if (parent[path[index]] is not JsonObject child)
                {
                    child = new JsonObject();
                    parent[path[index]] = child;
                }

                parent = child;
            }

            if (op == Remove)
            {
                parent.Remove(path[^1]);
            }
            else
            {
                parent[path[^1]] = operation["value"]?.DeepClone();
            }
        }

        return result;
    }

    public static string Compress(JsonNode node)
    {
        var bytes = Encoding.UTF8.GetBytes(node.ToJsonString());
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize))
        {
            gzip.Write(bytes);
        }

        return Convert.ToBase64String(output.ToArray());
    }

    public static JsonNode? Decompress(string compressed)
    {
        using var input = new MemoryStream(Convert.FromBase64String(compressed));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        return JsonNode.Parse(gzip);
    }

    private static void Walk(JsonNode? before, JsonNode? after, List<string> path, JsonArray operations)
    {
        if (before is JsonObject beforeObject && after is JsonObject afterObject)
        {
            foreach (var (key, value) in beforeObject)
            {
                if (!afterObject.ContainsKey(key))
                {
                    operations.Add(Operation(Remove, [.. path, key], null, includeValue: false));
                }
                else
                {
                    Walk(value, afterObject[key], [.. path, key], operations);
                }
            }

            foreach (var (key, value) in afterObject)
            {
                if (!beforeObject.ContainsKey(key))
                {
                    operations.Add(Operation(Set, [.. path, key], value, includeValue: true));
                }
            }

            return;
        }

        if (!JsonNode.DeepEquals(before, after))
        {
            operations.Add(Operation(Set, path, after, includeValue: true));
        }
    }

    private static JsonObject Operation(string op, List<string> path, JsonNode? value, bool includeValue)
    {
        var operation = new JsonObject
        {
            ["op"] = op,
            ["path"] = new JsonArray(path.Select(segment => (JsonNode?)JsonValue.Create(segment)).ToArray()),
        };

        if (includeValue)
        {
            operation["value"] = value?.DeepClone();
        }

        return operation;
    }
}
