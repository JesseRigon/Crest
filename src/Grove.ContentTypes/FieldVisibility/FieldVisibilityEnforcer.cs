#nullable enable

using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Models;

namespace Grove.ContentTypes;

/// <summary>
/// Applies visibility conditions at SAVE time: a field whose condition is false has
/// its value CLEARED (user ruling 2026-09-02) - the editor hides it live, but only
/// the server can guarantee a document never keeps a stale dependent value (an
/// un-voided invoice must not silently keep its void reason). Runs to a fixpoint,
/// because clearing one field can turn another field's condition false in the same
/// save (A visible-when B, B visible-when C).
/// </summary>
public sealed class FieldVisibilityEnforcer(
    IContentDefinitionManager contentDefinitionManager,
    IEnumerable<IFieldComparisonValueResolver> comparisonValues) : IFieldVisibilityEnforcer
{
    public async Task ClearHiddenFieldsAsync(ContentItem item, CancellationToken cancellationToken = default)
    {
        var type = await contentDefinitionManager.GetTypeDefinitionAsync(item.ContentType);
        if (type is null)
        {
            return;
        }

        var conditioned = new List<(string PartName, string FieldName, GroveFieldVisibilitySettings Settings)>();
        foreach (var typePart in type.Parts)
        {
            foreach (var field in typePart.PartDefinition.Fields)
            {
                var settings = field.GetSettings<GroveFieldVisibilitySettings>();
                if (settings is { HasCondition: true })
                {
                    conditioned.Add((typePart.Name, field.Name, settings));
                }
            }
        }

        if (conditioned.Count == 0)
        {
            return;
        }

        // Fixpoint: each pass may clear a field that another condition reads. Bounded
        // by the number of conditioned fields - a longer chain than that is a cycle,
        // and a cycle's remaining members legitimately keep their values.
        for (var pass = 0; pass < conditioned.Count; pass++)
        {
            var clearedAny = false;

            foreach (var (partName, fieldName, settings) in conditioned)
            {
                JsonObject? content = item.Content is JsonDynamicObject dynamicObject
                    ? (JsonObject)dynamicObject
                    : item.Content as JsonObject;

                if (content?[partName]?[fieldName] is null)
                {
                    continue;
                }

                // A bare "Field" path addresses the type's implicit part; the stored
                // JSON nests it under the part name, so qualify before reading.
                var contentPath = settings.Path!.Contains('.') ? settings.Path! : $"{item.ContentType}.{settings.Path}";
                var raw = ReadStoredValues(content, contentPath);
                var resolver = comparisonValues.LastOrDefault();
                var resolved = resolver is null
                    ? raw
                    : await resolver.ResolveAsync(item.ContentType, settings.Path!, null, raw, cancellationToken);

                if (!FieldVisibilityRules.IsVisible(settings, resolved))
                {
                    // Mutate the ROOT document directly - the same write style the
                    // save path itself uses. Alter<ContentPart> is unusable here:
                    // GetOrCreate can serve an element CACHED before the save
                    // replaced the part's subtree, so a mutation through it lands on
                    // a detached object and Apply's merge quietly resurrects the
                    // stale value.
                    if (content?[partName] is JsonObject partObject && partObject.Remove(fieldName))
                    {
                        clearedAny = true;
                    }
                }
            }

            if (!clearedAny)
            {
                return;
            }
        }
    }

    /// <summary>
    /// The values a field stores at <paramref name="path"/> ("Part.Field"): a reference field's
    /// <c>SelectedIds</c>, or a scalar field's <c>Text</c> or <c>Value</c>.
    /// </summary>
    public static IReadOnlyList<string> ReadStoredValues(JsonNode? content, string path)
    {
        var node = content;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            node = node?[segment];
        }

        if (node is null)
        {
            return [];
        }

        if (node is JsonValue)
        {
            return [node.ToString()];
        }

        if (node is not JsonObject field)
        {
            return [];
        }

        if (field["SelectedIds"] is JsonArray selected)
        {
            return [.. selected.Select(entry => entry?.ToString()).Where(entry => !string.IsNullOrWhiteSpace(entry))!];
        }

        var scalar = field["Text"] ?? field["Value"];
        return scalar is null ? [] : [scalar.ToString()];
    }
}
