using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Records;

namespace Grove.Contents.Management;

/// <summary>
/// A snapshot embedding's stored schema (docs/content-engine.md › Live and snapshot embeddings):
/// <c>{ "Types": { "&lt;Type&gt;": { … } }, "Items": [ "&lt;id&gt;", … ] }</c> beside the items. Each
/// type is written as its definition was when the parent was published (its field sets, fields,
/// field types and settings, and its own embeddings); <c>Items</c> names the items frozen then.
/// A frozen item reads through <see cref="Read"/> (<see cref="IEmbeddedContentReader"/>), never
/// through the current definition.
/// </summary>
public static class EmbeddedContentSchema
{
    public const string TypesKey = "Types";
    public const string ItemsKey = "Items";

    /// <summary>The schema entry of <paramref name="type"/>.</summary>
    public static JsonObject Write(ContentTypeDefinition type) => new()
    {
        ["DisplayName"] = type.DisplayName,
        ["Settings"] = type.Settings?.DeepClone(),
        ["FieldSets"] = new JsonArray([.. type.FieldSets.Select(fieldSet => (JsonNode)new JsonObject
        {
            ["Name"] = fieldSet.Name,
            ["FieldSetName"] = fieldSet.FieldSetDefinition.Name,
            ["Settings"] = fieldSet.Settings?.DeepClone(),
            ["FieldSetSettings"] = fieldSet.FieldSetDefinition.Settings?.DeepClone(),
            ["Fields"] = new JsonArray([.. fieldSet.FieldSetDefinition.Fields.Select(field => (JsonNode)new JsonObject
            {
                ["Name"] = field.Name,
                ["FieldType"] = field.FieldTypeDefinition.Name,
                ["Settings"] = field.Settings?.DeepClone(),
            })]),
        })]),
        ["Embeddings"] = JArray.FromObject(type.Embeddings.Select(embedding => embedding.ToRecord()).ToArray()),
    };

    /// <summary>The definition <paramref name="name"/> had when its schema entry was written.</summary>
    public static ContentTypeDefinition Read(string name, JsonObject entry)
    {
        var fieldSets = (entry["FieldSets"] as JsonArray ?? []).OfType<JsonObject>().Select(fieldSet => new ContentTypeFieldSetDefinition(
            fieldSet["Name"]?.ToString(),
            new ContentFieldSetDefinition(
                fieldSet["FieldSetName"]?.ToString(),
                (fieldSet["Fields"] as JsonArray ?? []).OfType<JsonObject>().Select(field => new ContentFieldDefinition(
                    new ContentFieldTypeDefinition(field["FieldType"]?.ToString()),
                    field["Name"]?.ToString(),
                    field["Settings"]?.DeepClone() as JsonObject ?? [])),
                fieldSet["FieldSetSettings"]?.DeepClone() as JsonObject ?? []),
            fieldSet["Settings"]?.DeepClone() as JsonObject ?? []));

        var embeddings = (entry["Embeddings"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(embedding => new EmbeddedContentDefinition(embedding.ToObject<EmbeddedContentDefinitionRecord>()));

        return new ContentTypeDefinition(name, entry["DisplayName"]?.ToString(), fieldSets, entry["Settings"]?.DeepClone() as JsonObject ?? [], embeddings);
    }

    /// <summary>Every type a stored schema holds, by name.</summary>
    public static IReadOnlyDictionary<string, ContentTypeDefinition> ReadAll(JsonObject schema) =>
        (schema?[TypesKey] as JsonObject ?? [])
            .Where(pair => pair.Value is JsonObject)
            .ToDictionary(pair => pair.Key, pair => Read(pair.Key, (JsonObject)pair.Value), StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether <paramref name="contentItemId"/> was frozen by the schema's last publish.</summary>
    public static bool IsFrozen(JsonObject schema, string contentItemId) =>
        schema?[ItemsKey] is JsonArray items && items.Any(item => string.Equals(item?.ToString(), contentItemId, StringComparison.Ordinal));
}
