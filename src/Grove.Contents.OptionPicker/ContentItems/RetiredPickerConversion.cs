using System.Text.Json.Nodes;
using Grove.Infrastructure.Settings;
using Grove.Models;

namespace Grove.Contents.OptionPicker.ContentItems;

/// <summary>
/// The conversion of the retired picker fields into option list fields (docs/content-engine.md ›
/// Picking is one field type), for recipes on the way in: a taxonomy field becomes an option list
/// field over its taxonomy's terms (<c>embedded:Taxonomy.Terms</c> filtered on the taxonomy, leaves
/// only when it asked, adding tags when it was open), a user picker one over <c>users</c> (limited
/// to its roles unless it showed every user), a localization set picker one over
/// <c>localization-set:</c> its types. Their values become <c>SelectedIds</c>. Pure.
/// </summary>
public static class RetiredPickerConversion
{
    public const string TaxonomyField = "TaxonomyField";

    public const string UserPickerField = "UserPickerField";

    public const string LocalizationSetField = "LocalizationSetContentPickerField";

    /// <summary>The type a taxonomy field's taxonomy is (the taxonomy module's type).</summary>
    public const string TaxonomyType = "Taxonomy";

    /// <summary>The embedding a taxonomy's terms are.</summary>
    public const string TermsEmbedding = "Terms";

    // By retired field: its settings section and the stored property its ids are in.
    private static readonly Dictionary<string, (string Settings, string Ids)> s_retired = new(StringComparer.Ordinal)
    {
        [TaxonomyField] = ("TaxonomyFieldSettings", "TermContentItemIds"),
        [UserPickerField] = ("UserPickerFieldSettings", "UserIds"),
        [LocalizationSetField] = ("LocalizationSetContentPickerFieldSettings", "LocalizationSets"),
    };

    /// <summary>Whether <paramref name="fieldType"/> is one of the retired picker fields.</summary>
    public static bool IsRetired(string? fieldType) => fieldType is not null && s_retired.ContainsKey(fieldType);

    /// <summary>A retired picker field's settings as an option list field's; every other section is kept.</summary>
    public static JsonObject ConvertSettings(string fieldType, JsonObject? settings)
    {
        var converted = settings?.DeepClone().AsObject() ?? [];
        var section = s_retired[fieldType].Settings;
        var retired = converted[section] as JsonObject;
        converted.Remove(section);
        var tagsOpen = converted["TaxonomyFieldTagsEditorSettings"]?["Open"] is JsonValue open && open.TryGetValue<bool>(out var isOpen) && isOpen;
        converted.Remove("TaxonomyFieldTagsEditorSettings");

        // The retired editors (a tags editor, a user search) are the option picker now.
        if (converted["ContentFieldSettings"] is JsonObject fieldSettings && fieldSettings["Editor"]?.GetValue<string>() is { Length: > 0 })
        {
            fieldSettings.Remove("Editor");
        }

        var option = new JsonObject
        {
            [nameof(OptionListFieldSettings.Required)] = Bool(retired?["Required"]),
            [nameof(OptionListFieldSettings.Placeholder)] = retired?["Placeholder"]?.GetValue<string>() ?? string.Empty,
        };
        if (retired?["Hint"]?.GetValue<string>() is { Length: > 0 } hint)
        {
            option[nameof(OptionListFieldSettings.Hint)] = hint;
        }

        var filters = new JsonArray();
        switch (fieldType)
        {
            case TaxonomyField:
                option[nameof(OptionListFieldSettings.SourceKey)] = OptionSourceKeys.ForEmbedding(TaxonomyType, TermsEmbedding);
                option[nameof(OptionListFieldSettings.Multiple)] = !Bool(retired?["Unique"]);
                option[nameof(OptionListFieldSettings.AllowCreate)] = Bool(retired?["Open"]) || tagsOpen;
                filters.Add(Filter("Parent", retired?["TaxonomyContentItemId"]?.GetValue<string>() ?? string.Empty));
                if (Bool(retired?["LeavesOnly"]))
                {
                    filters.Add(Filter("Leaf", "true"));
                }

                break;
            case UserPickerField:
                option[nameof(OptionListFieldSettings.SourceKey)] = OptionSourceKeys.UserProvider;
                option[nameof(OptionListFieldSettings.Multiple)] = Bool(retired?["Multiple"]);
                var displayAll = retired?["DisplayAllUsers"] is not JsonValue all || !all.TryGetValue<bool>(out var allUsers) || allUsers;
                if (!displayAll)
                {
                    filters.Add(Filter("Roles", string.Join(',', Strings(retired?["DisplayedRoles"])), OptionFilterOperators.In));
                }

                break;
            case LocalizationSetField:
                option[nameof(OptionListFieldSettings.SourceKey)] = OptionSourceKeys.ForLocalizationSets(Strings(retired?["DisplayedContentTypes"]));
                option[nameof(OptionListFieldSettings.Multiple)] = Bool(retired?["Multiple"]);
                break;
        }

        if (filters.Count > 0)
        {
            option[nameof(OptionListFieldSettings.Filters)] = filters;
        }

        converted[nameof(OptionListFieldSettings)] = option;
        return converted;
    }

    /// <summary>Converts the retired picker fields of a recipe's content definition step in place. True when anything changed.</summary>
    public static bool ConvertDefinitionStep(JsonObject step)
    {
        var changed = false;
        foreach (var fieldSet in (step["ContentFieldSets"] as JsonArray ?? []).OfType<JsonObject>())
        {
            foreach (var field in (fieldSet["ContentFieldDefinitionRecords"] as JsonArray ?? []).OfType<JsonObject>())
            {
                if (field["FieldName"]?.GetValue<string>() is not { } fieldType || !IsRetired(fieldType))
                {
                    continue;
                }

                field["FieldName"] = ContentPickerConversion.FieldType;
                field["Settings"] = ConvertSettings(fieldType, field["Settings"] as JsonObject);
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Converts every stored value in a retired shape anywhere in a JSON tree (an item in a
    /// recipe's content step): its ids become <c>SelectedIds</c>; readers take the source from the
    /// field's settings. True when anything changed.
    /// </summary>
    public static bool ConvertValues(JsonNode? node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject value:
                if (ConvertValue(value))
                {
                    return true;
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

    private static bool ConvertValue(JsonObject value)
    {
        var ids = value["TermContentItemIds"] as JsonArray ?? value["UserIds"] as JsonArray ?? value["LocalizationSets"] as JsonArray;
        if (ids is null)
        {
            return false;
        }

        value["SelectedIds"] = ids.DeepClone();
        foreach (var retired in new[] { "TermContentItemIds", "TaxonomyContentItemId", "TagNames", "UserIds", "UserNames", "LocalizationSets" })
        {
            value.Remove(retired);
        }

        return true;
    }

    private static JsonObject Filter(string path, string value, string op = OptionFilterOperators.Equals) => new()
    {
        [nameof(OptionFilter.Path)] = path,
        [nameof(OptionFilter.Operator)] = op,
        [nameof(OptionFilter.Value)] = value,
    };

    private static List<string> Strings(JsonNode? node) =>
        node is JsonArray array
            ? [.. array.Select(entry => entry?.GetValue<string>()).Where(entry => !string.IsNullOrWhiteSpace(entry)).Select(entry => entry!)]
            : [];

    private static bool Bool(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;
}
