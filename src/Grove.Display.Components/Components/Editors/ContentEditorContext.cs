using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Display.Components.Models;
using Grove.Display.Management.Components;

namespace Grove.Display.Components.Editors;

/// <summary>
/// One content item being edited (docs/blazor-display.md § 7, rework step 5): its content JSON,
/// its type's parts and fields, the per-field errors, and the rules that cross fields: a field's
/// visibility condition and a dependent option picker's cascade both read other fields' live
/// values. Every editor component reads and writes through it (cascaded), whether the editors
/// were laid out by the server's editor tree or by <c>GroveFormManager</c>, so there is one
/// editing behaviour.
/// </summary>
public sealed class ContentEditorContext(JsonNode? content, GroveContentItemMetadata? metadata, HttpClient? http)
{
    // Resolved parent comparison values, keyed by "path|ids". A null value means a resolve
    // request is in flight; the field stays hidden until it lands rather than flashing into
    // view with an unverified condition.
    private readonly Dictionary<string, IReadOnlyList<string>?> _parentKeys = new(StringComparer.Ordinal);

    // Each field's parsed visibility condition, by the field's metadata.
    private readonly Dictionary<GroveContentFieldMetadata, DisplayBinding?> _conditions = new(ReferenceEqualityComparer.Instance);

    private static readonly JsonSerializerOptions ConditionJson = new(JsonSerializerDefaults.Web);

    public JsonObject Content { get; } = content as JsonObject ?? (content is null ? [] : content.AsObject());

    public GroveContentItemMetadata? Metadata { get; } = metadata;

    /// <summary>The edited item's id; null for an item not saved yet (its editors that act on the stored item wait for the save).</summary>
    public string? ContentItemId { get; init; }

    /// <summary>The edited item's type.</summary>
    public string? ContentType => Metadata?.ContentType;

    /// <summary>The HTTP client the editors call the APIs with (none in a test).</summary>
    public HttpClient? Http => http;

    public bool Disabled { get; set; }

    public Dictionary<string, string> Errors { get; } = new(StringComparer.Ordinal);

    /// <summary>Raised when a value changes or a resolve lands: editors re-render, the page sees the content.</summary>
    public event Action? Changed;

    public static string Key(string fieldSetName, string? fieldName) => fieldName is null ? fieldSetName : $"{fieldSetName}.{fieldName}";

    public GroveContentFieldSetMetadata? FieldSet(string fieldSetName) =>
        Metadata?.FieldSets?.FirstOrDefault(part => string.Equals(part.Name, fieldSetName, StringComparison.OrdinalIgnoreCase));

    public GroveContentFieldMetadata? Field(string fieldSetName, string fieldName) =>
        FieldSet(fieldSetName)?.Fields.FirstOrDefault(field => string.Equals(field.Name, fieldName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The stored object of a field (<c>{ "Text": ... }</c>), or of a part when <paramref name="fieldName"/> is null.</summary>
    public JsonObject? Get(string fieldSetName, string? fieldName) =>
        (fieldName is null ? Content[fieldSetName] : Content[fieldSetName]?[fieldName]) as JsonObject;

    /// <summary>The stored object, created when missing, for an editor to write into.</summary>
    public JsonObject GetOrCreate(string fieldSetName, string? fieldName)
    {
        if (Content[fieldSetName] is not JsonObject part)
        {
            Content[fieldSetName] = part = [];
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
    public void Set(string fieldSetName, string? fieldName, string property, JsonNode? value)
    {
        GetOrCreate(fieldSetName, fieldName)[property] = value?.DeepClone();
        Errors.Remove(Key(fieldSetName, fieldName));

        // A parent's committed value feeds dependent pickers and visibility conditions: drop the
        // resolved keys so both re-evaluate.
        _parentKeys.Clear();
        Changed?.Invoke();
    }

    /// <summary>Tells everyone a field's (or part's) object was changed in place.</summary>
    public void Touch(string fieldSetName, string? fieldName)
    {
        Errors.Remove(Key(fieldSetName, fieldName));
        _parentKeys.Clear();
        Changed?.Invoke();
    }

    /// <summary>Tells everyone the errors changed (the server's, after a save).</summary>
    public void Refresh() => Changed?.Invoke();

    public void Set<T>(string fieldSetName, string? fieldName, string property, T value) =>
        Set(fieldSetName, fieldName, property, value is null ? null : JsonSerializer.SerializeToNode(value));

    /// <summary>Checks the required fields that are visible; the errors go to <see cref="Errors"/>.</summary>
    public bool Validate()
    {
        Errors.Clear();
        foreach (var part in Metadata?.FieldSets ?? [])
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

    private JsonNode? PrimaryValue(string partName, GroveContentFieldMetadata field)
    {
        var propertyName = field.FieldType switch
        {
            "OptionListField" => "SelectedIds",
            "TextField" => "Text",
            "HtmlField" => "Html",
            "MarkdownField" => "Markdown",
            "LinkField" => "Url",
            _ => "Value",
        };

        return Content[partName]?[field.Name]?[propertyName];
    }

    // --- Option pickers -------------------------------------------------------

    public static JsonNode? PickerSettings(GroveContentFieldMetadata field) => field.Settings?["OptionListFieldSettings"];

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
    public IReadOnlyDictionary<string, string[]>? EditorState(GroveContentFieldMetadata field)
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

    // Paths are "Field" (the type's implicit part) or "FieldSet.Field", matching the server's
    // convention. A picker contributes its selected ids, scalars their value.
    private string[] ReadPathValues(string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length is 0 or > 2)
        {
            return [];
        }

        // A bare field name is a field of the type's own field set.
        return [.. FieldConditions.StoredValues(Content, segments.Length == 2 ? path : $"{Metadata?.ContentType}.{path}")];
    }

    // --- Field visibility -----------------------------------------------------

    /// <summary>
    /// Evaluates a field's visibility condition (<c>FieldVisibilitySettings.When</c>) against the
    /// live form, with the evaluator the save uses (<see cref="FieldConditions"/>). A scalar field
    /// compares its stored value; an option list field's ids resolve to option keys through the
    /// server (conditions name keys, never ids or labels). While a resolve is in flight the field
    /// stays hidden rather than flashing into view on an unverified condition.
    /// </summary>
    public bool IsFieldVisible(string fieldSetName, GroveContentFieldMetadata field)
    {
        if (VisibilityCondition(fieldSetName, field) is not { } when)
        {
            return true;
        }

        var values = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
        var ignoreCase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in FieldConditions.Fields(when).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var raw = ReadPathValues(path);
            var resolved = raw.Length == 0 ? raw : ParentComparisonValues(path, raw);
            if (resolved is null)
            {
                return false;
            }

            // Option keys compare case-insensitively: lower-cased here and in the condition's literals.
            if (IsOptionField(path))
            {
                ignoreCase.Add(path);
                resolved = [.. resolved.Select(value => value.ToLowerInvariant())];
            }

            values[path] = FieldConditions.Value(resolved);
        }

        if (ignoreCase.Count > 0)
        {
            when = FieldConditions.IgnoreCase(when, ignoreCase.Contains);
        }

        return FieldConditions.IsMet(when, path => values.GetValueOrDefault(path)?.DeepClone());
    }

    private bool IsOptionField(string path)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 2 && string.Equals(Field(segments[0], segments[1])?.FieldType, "OptionListField", StringComparison.Ordinal);
    }

    // The field's condition, its sibling paths read from the attachment it is edited under.
    private DisplayBinding? VisibilityCondition(string fieldSetName, GroveContentFieldMetadata field)
    {
        if (!_conditions.TryGetValue(field, out var when))
        {
            try
            {
                when = field.Settings?["FieldVisibilitySettings"]?["When"] is JsonObject condition
                    && condition.Deserialize<DisplayBinding>(ConditionJson) is { } parsed
                    ? FieldConditions.Qualify(parsed, fieldSetName)
                    : null;
            }
            catch (JsonException)
            {
                when = null;
            }

            _conditions[field] = when;
        }

        return when;
    }

    private IReadOnlyList<string>? ParentComparisonValues(string path, string[] raw)
    {
        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var parentField = segments.Length is 1 or 2
            ? Field(segments.Length == 2 ? segments[0] : Metadata?.ContentType ?? string.Empty, segments[^1])
            : null;

        if (parentField is null || !string.Equals(parentField.FieldType, "OptionListField", StringComparison.Ordinal))
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

            var response = await http.PostAsJsonAsync("api/grove/option-sources/resolve", new
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
