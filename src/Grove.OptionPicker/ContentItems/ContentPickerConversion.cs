using System.Text.Json.Nodes;
using Grove.ContentManagement.Metadata.Models;
using Grove.Models;
using Grove.Settings;

namespace Grove.OptionPicker.ContentItems;

/// <summary>
/// The conversion of the retired content picker field into an option picker field over the
/// content items source (docs/server.md § 3): one field type for picking. Used by the data
/// migration that converts a tenant's definitions and items, and by the recipe mapping that
/// converts old recipes on the way in. Pure, so both paths convert the same way.
/// </summary>
public static class ContentPickerConversion
{
    /// <summary>The retired field type's name, as stored definitions and old recipes name it.</summary>
    public const string RetiredFieldType = "ContentPickerField";

    public const string FieldType = "OptionPickerField";

    private const string RetiredSettings = "ContentPickerFieldSettings";

    // The retired field's index-backed search editors carried their own settings sections.
    private static readonly string[] RetiredEditorSettings = ["ContentPickerFieldLuceneEditorSettings", "ContentPickerFieldElasticEditorSettings"];

    /// <summary>The default title pattern of the retired field: the item's display text.</summary>
    public const string DefaultTitlePattern = "{{ Model.ContentItem | display_text }}";

    /// <summary>
    /// A content picker field's settings as an option picker's: the content items source over the
    /// allowed types and stereotypes (or every type without a stereotype), Multiple, Required,
    /// Hint, Placeholder and the title pattern (dropped when it is the default). Every other
    /// section (position, display name, a lock, a visibility condition) is kept; the retired
    /// editor modes (Lucene, Elasticsearch) fall back to the default editor.
    /// </summary>
    public static JsonObject ConvertSettings(JsonObject? settings)
    {
        var converted = settings?.DeepClone().AsObject() ?? [];
        var picker = converted[RetiredSettings] as JsonObject;
        converted.Remove(RetiredSettings);
        foreach (var section in RetiredEditorSettings)
        {
            converted.Remove(section);
        }

        if (converted["ContentPartFieldSettings"] is JsonObject partField
            && partField["Editor"]?.GetValue<string>() is "Lucene" or "Elasticsearch")
        {
            partField.Remove("Editor");
        }

        var types = Strings(picker?["DisplayedContentTypes"]);
        var stereotypes = Strings(picker?["DisplayedStereotypes"]);
        var all = Bool(picker?["DisplayAllContentTypes"]);
        var titlePattern = picker?["TitlePattern"]?.GetValue<string>();

        var option = new JsonObject
        {
            [nameof(OptionPickerFieldSettings.SourceKey)] = OptionSourceKeys.ForContentItems(types, stereotypes, all),
            [nameof(OptionPickerFieldSettings.Multiple)] = Bool(picker?["Multiple"]),
            [nameof(OptionPickerFieldSettings.Required)] = Bool(picker?["Required"]),
            [nameof(OptionPickerFieldSettings.Placeholder)] = picker?["Placeholder"]?.GetValue<string>() ?? string.Empty,
        };

        if (picker?["Hint"]?.GetValue<string>() is { Length: > 0 } hint)
        {
            option[nameof(OptionPickerFieldSettings.Hint)] = hint;
        }

        if (!string.IsNullOrWhiteSpace(titlePattern) && !string.Equals(titlePattern.Trim(), DefaultTitlePattern, StringComparison.Ordinal))
        {
            option[nameof(OptionPickerFieldSettings.TitlePattern)] = titlePattern;
        }

        converted[nameof(OptionPickerFieldSettings)] = option;
        return converted;
    }

    /// <summary>
    /// A stored field value in the retired shape (<c>ContentItemIds</c>) as an option picker's
    /// (<c>SelectedIds</c> and the source key). True when the value changed.
    /// </summary>
    public static bool ConvertValue(JsonObject? field, string sourceKey)
    {
        if (field?["ContentItemIds"] is not JsonArray ids)
        {
            return false;
        }

        if (field["SelectedIds"] is not JsonArray { Count: > 0 })
        {
            field["SelectedIds"] = ids.DeepClone();
        }

        if (!string.IsNullOrEmpty(sourceKey))
        {
            field["SourceKey"] = sourceKey;
        }

        field.Remove("ContentItemIds");
        return true;
    }

    /// <summary>
    /// Converts every stored value in the retired shape anywhere in a JSON tree (an item in a
    /// recipe's content step, with its contained items), without definitions: the value keeps no
    /// source key of its own and readers take it from the field's settings. True when anything
    /// changed.
    /// </summary>
    public static bool ConvertValues(JsonNode? node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject value:
                if (value["ContentItemIds"] is JsonArray)
                {
                    return ConvertValue(value, string.Empty);
                }

                foreach (var (_, child) in value.ToArray())
                {
                    changed |= ConvertValues(child);
                }

                break;
            case JsonArray array:
                foreach (var child in array.ToArray())
                {
                    changed |= ConvertValues(child);
                }

                break;
        }

        return changed;
    }

    /// <summary>
    /// Converts the field definitions of a recipe's content definition step (its parts' field
    /// records) in place. True when anything changed.
    /// </summary>
    public static bool ConvertDefinitionStep(JsonObject step)
    {
        var changed = false;
        foreach (var part in (step["ContentParts"] as JsonArray ?? []).OfType<JsonObject>())
        {
            foreach (var field in (part["ContentPartFieldDefinitionRecords"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (field["FieldName"]?.GetValue<string>() != RetiredFieldType)
                {
                    continue;
                }

                field["FieldName"] = FieldType;
                field["Settings"] = ConvertSettings(field["Settings"] as JsonObject);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Converts an item's stored picker values: every option picker field of its type whose value
    /// is still in the retired shape, then every item contained in it (bag elements), by their own
    /// types. True when anything changed.
    /// </summary>
    public static bool ConvertItem(JsonObject? content, string contentType, Func<string, ContentTypeDefinition?> types)
    {
        if (content is null)
        {
            return false;
        }

        var changed = false;
        if (types(contentType) is { } type)
        {
            foreach (var part in type.Parts)
            {
                if (content[part.Name] is not JsonObject partContent)
                {
                    continue;
                }

                foreach (var field in part.PartDefinition.Fields.Where(field => field.FieldDefinition.Name == FieldType))
                {
                    var sourceKey = field.GetSettings<OptionPickerFieldSettings>().SourceKey;
                    changed |= ConvertValue(partContent[field.Name] as JsonObject, sourceKey);
                }
            }
        }

        foreach (var (nestedType, nested) in ContainedItems(content))
        {
            changed |= ConvertItem(nested, nestedType, types);
        }

        return changed;
    }

    // Items stored inside an item (a bag's elements): objects carrying their own type and id.
    private static IEnumerable<(string ContentType, JsonObject Content)> ContainedItems(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject value:
                foreach (var (_, child) in value)
                {
                    if (child is JsonObject candidate
                        && candidate["ContentType"] is JsonValue typeValue && typeValue.TryGetValue<string>(out var type)
                        && candidate["ContentItemId"] is JsonValue)
                    {
                        yield return (type, candidate);
                        continue;
                    }

                    foreach (var nested in ContainedItems(child))
                    {
                        yield return nested;
                    }
                }

                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    if (child is JsonObject candidate
                        && candidate["ContentType"] is JsonValue typeValue && typeValue.TryGetValue<string>(out var type)
                        && candidate["ContentItemId"] is JsonValue)
                    {
                        yield return (type, candidate);
                        continue;
                    }

                    foreach (var nested in ContainedItems(child))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static List<string> Strings(JsonNode? node) =>
        node is JsonArray array
            ? [.. array.Select(entry => entry?.GetValue<string>()).Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry!)]
            : [];

    private static bool Bool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
}
