using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Crest.Components.Models;
using Crest.Components.Options;

namespace Crest.Components.Editors;

/// <summary>
/// One content item being edited (docs/blazor-display.md § 7, rework step 5): its content JSON,
/// its type's parts and fields, the per-field errors, and the rules that cross fields: a field's
/// visibility condition and a dependent option picker's cascade both read other fields' live
/// values. Every editor component reads and writes through it (cascaded), whether the editors
/// were laid out by the server's editor tree or by <c>CrestFormManager</c>, so there is one
/// editing behaviour.
/// </summary>
public sealed class ContentEditorContext(JsonNode? content, CrestContentItemMetadata? metadata, HttpClient? http)
{
    // Resolved parent comparison values, keyed by "path|ids". A null value means a resolve
    // request is in flight; the field stays hidden until it lands rather than flashing into
    // view with an unverified condition.
    private readonly Dictionary<string, IReadOnlyList<string>?> _parentKeys = new(StringComparer.Ordinal);

    public JsonObject Content { get; } = content as JsonObject ?? (content is null ? [] : content.AsObject());

    public CrestContentItemMetadata? Metadata { get; } = metadata;

    public bool Disabled { get; set; }

    public Dictionary<string, string> Errors { get; } = new(StringComparer.Ordinal);

    /// <summary>Raised when a value changes or a resolve lands: editors re-render, the page sees the content.</summary>
    public event Action? Changed;

    public static string Key(string partName, string? fieldName) => fieldName is null ? partName : $"{partName}.{fieldName}";

    public CrestContentPartMetadata? Part(string partName) =>
        Metadata?.Parts?.FirstOrDefault(part => string.Equals(part.Name, partName, StringComparison.OrdinalIgnoreCase));

    public CrestContentFieldMetadata? Field(string partName, string fieldName) =>
        Part(partName)?.Fields.FirstOrDefault(field => string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The stored object of a field (<c>{ "Text": ... }</c>), or of a part when <paramref name="fieldName"/> is null.</summary>
    public JsonObject? Get(string partName, string? fieldName) =>
        (fieldName is null ? Content[partName] : Content[partName]?[fieldName]) as JsonObject;

    /// <summary>The stored object, created when missing, for an editor to write into.</summary>
    public JsonObject GetOrCreate(string partName, string? fieldName)
    {
        if (Content[partName] is not JsonObject part)
        {
            Content[partName] = part = [];
        }

        if (fieldName is null)
        {
            return part;
        }

        if (part[fieldName] is not JsonObject field)
        {
            part[fieldName] = field = [];
        }

        return field;
    }

    /// <summary>Sets one property of a field's (or part's) object and tells everyone.</summary>
    public void Set(string partName, string? fieldName, string property, JsonNode? value)
    {
        GetOrCreate(partName, fieldName)[property] = value?.DeepClone();
        Errors.Remove(Key(partName, fieldName));

        // A parent's committed value feeds dependent pickers and visibility conditions: drop the
        // resolved keys so both re-evaluate.
        _parentKeys.Clear();
        Changed?.Invoke();
    }

    /// <summary>Tells everyone a field's (or part's) object was changed in place.</summary>
    public void Touch(string partName, string? fieldName)
    {
        Errors.Remove(Key(partName, fieldName));
        _parentKeys.Clear();
        Changed?.Invoke();
    }

    public void Set<T>(string partName, string? fieldName, string property, T value) =>
        Set(partName, fieldName, property, value is null ? null : JsonSerializer.SerializeToNode(value));

    /// <summary>Checks the required fields that are visible; the errors go to <see cref="Errors"/>.</summary>
    public bool Validate()
    {
        Errors.Clear();
        foreach (var part in Metadata?.Parts ?? [])
        {
            foreach (var field in part.Fields)
            {
                // A hidden field cannot be required: the user cannot see it, and the server clears
                // its value on save anyway.
                if (!field.Required || !IsFieldVisible(part.Name, field))
                {
                    continue;
                }

                var value = PrimaryValue(part.Name, field);
                var isMissing = value is null
                    || value.GetValueKind() is JsonValueKind.Null
                    || value.GetValueKind() is JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetValue<string>())
                    || value is JsonArray { Count: 0 };

                if (isMissing)
                {
                    Errors[Key(part.Name, field.Name)] = $"{field.Label} is required.";
                }
            }
        }

        Changed?.Invoke();
        return Errors.Count == 0;
    }

    private JsonNode? PrimaryValue(string partName, CrestContentFieldMetadata field)
    {
        var propertyName = field.FieldType switch
        {
            "ContentPickerField" => "ContentItemIds",
            "OptionPickerField" => "SelectedIds",
            "TextField" => "Text",
            "HtmlField" => "Html",
            "MarkdownField" => "Markdown",
            "LinkField" => "Url",
            _ => "Value",
        };

        return Content[partName]?[field.Name]?[propertyName];
    }

    // --- Option pickers -------------------------------------------------------

    public static JsonNode? PickerSettings(CrestContentFieldMetadata field) => field.Settings?["OptionPickerFieldSettings"];

    public static IReadOnlyList<OptionPickerColumn> PickerColumns(JsonNode? pickerSettings)
    {
        if (pickerSettings?["Columns"] is not JsonArray columns)
        {
            return [];
        }

        return [.. columns
            .Select(column => column is null ? null : new OptionPickerColumn(
                column["Path"]?.GetValue<string>() ?? string.Empty,
                column["Label"]?.GetValue<string>() ?? column["Path"]?.GetValue<string>() ?? string.Empty,
                column["Width"]?.GetValue<string>(),
                column["Visible"]?.GetValue<bool>() ?? true,
                column["InChip"]?.GetValue<bool>() ?? false))
            .Where(column => column is not null && column.Path.Length > 0)
            .Select(column => column!)];
    }

    /// <summary>
    /// The dependency values a picker's configured filters read (by ValueFrom), so the server can
    /// resolve its cascade. A change of a parent re-renders the picker, which re-queries.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? EditorState(CrestContentFieldMetadata field)
    {
        if (PickerSettings(field)?["Filters"] is not JsonArray filters || filters.Count == 0)
        {
            return null;
        }

        var state = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var filter in filters)
        {
            var valueFrom = filter?["ValueFrom"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(valueFrom) || state.ContainsKey(valueFrom))
            {
                continue;
            }

            state[valueFrom] = ReadPathValues(valueFrom);
        }

        return state.Count == 0 ? null : state;
    }

    // Paths are "Field" (the type's implicit part) or "Part.Field", matching the server's
    // convention. A picker contributes its selected ids, scalars their value.
    private string[] ReadPathValues(string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is 0 or > 2)
        {
            return [];
        }

        var partName = segments.Length == 2 ? segments[0] : Metadata?.ContentType ?? string.Empty;
        var node = Content[partName]?[segments[^1]];
        if (node is null)
        {
            return [];
        }

        if (node["SelectedIds"] is JsonArray ids)
        {
            return [.. ids.Select(id => id?.ToString()).Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!)];
        }

        var scalar = (node["Text"] ?? node["Value"])?.ToString();
        return string.IsNullOrWhiteSpace(scalar) ? [] : [scalar];
    }

    // --- Field visibility -----------------------------------------------------

    /// <summary>
    /// Evaluates a field's CrestFieldVisibilitySettings against the live form. Scalar parents
    /// compare their stored value; a picker parent's ids resolve to option KEYS through the server
    /// (the identity rule: conditions name keys, never ids or labels).
    /// </summary>
    public bool IsFieldVisible(string partName, CrestContentFieldMetadata field)
    {
        var settings = field.Settings?["CrestFieldVisibilitySettings"];
        var path = settings?["Path"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var raw = ReadPathValues(path);
        var @operator = settings?["Operator"]?.GetValue<string>() ?? "Any";
        if (string.Equals(@operator, "Any", StringComparison.OrdinalIgnoreCase))
        {
            return raw.Length > 0;
        }

        if (raw.Length == 0)
        {
            return false;
        }

        string[] keys = settings?["Keys"] is JsonArray keyArray
            ? [.. keyArray.Select(key => key?.GetValue<string>()).Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => key!)]
            : [];
        var parentValues = ParentComparisonValues(path, raw);

        return parentValues is not null
            && parentValues.Any(value => keys.Any(key => string.Equals(key, value, StringComparison.OrdinalIgnoreCase)));
    }

    private IReadOnlyList<string>? ParentComparisonValues(string path, string[] raw)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var parentField = segments.Length is 1 or 2
            ? Field(segments.Length == 2 ? segments[0] : Metadata?.ContentType ?? string.Empty, segments[^1])
            : null;

        if (parentField is null || !string.Equals(parentField.FieldType, "OptionPickerField", StringComparison.Ordinal))
        {
            // Scalar parents: the stored value IS the comparison value.
            return raw;
        }

        var sourceKey = PickerSettings(parentField)?["SourceKey"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(sourceKey))
        {
            return [];
        }

        var cacheKey = $"{path}|{string.Join(',', raw)}";
        if (_parentKeys.TryGetValue(cacheKey, out var resolved))
        {
            return resolved;
        }

        // Mark in flight and resolve in the background; the change on completion re-evaluates
        // visibility with real keys.
        _parentKeys[cacheKey] = null;
        _ = ResolveParentKeysAsync(cacheKey, sourceKey, raw);
        return null;
    }

    private async Task ResolveParentKeysAsync(string cacheKey, string sourceKey, string[] ids)
    {
        try
        {
            if (http is null)
            {
                _parentKeys[cacheKey] = [];
                return;
            }

            var response = await http.PostAsJsonAsync("api/crest/option-sources/resolve", new
            {
                SourceKey = sourceKey,
                Ids = ids,
                Columns = Array.Empty<string>(),
            });

            if (response.IsSuccessStatusCode)
            {
                var rows = await response.Content.ReadFromJsonAsync<OptionPickerRow[]>() ?? [];
                _parentKeys[cacheKey] = [.. rows
                    .Select(row => string.IsNullOrWhiteSpace(row.Key) ? row.Id : row.Key!)
                    .Where(value => !string.IsNullOrWhiteSpace(value))];
            }
            else
            {
                // Fail closed: an unresolvable parent must not reveal the field.
                _parentKeys[cacheKey] = [];
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            _parentKeys[cacheKey] = [];
        }

        Changed?.Invoke();
    }

    /// <summary>Reads a stored date the way the editors write it (round-trip, invariant).</summary>
    public static DateTime? ReadDate(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        if (node.GetValueKind() == JsonValueKind.String && DateTime.TryParse(node.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
        {
            return parsed;
        }

        return node.GetValue<DateTime>();
    }
}
