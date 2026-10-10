using System.Globalization;
using System.Text.Json;
using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;

namespace Crest.Workflows.Contents;

/// <summary>
/// A field on a content item, as a flow names it: <c>Part.Field</c>, where Part is the
/// type's part name (a type's own fields sit under a part named like the type).
/// </summary>
public readonly record struct FieldPath(string Part, string Field)
{
    public static bool TryParse(string? text, out FieldPath path)
    {
        path = default;
        var parts = text?.Trim().Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts is not { Length: 2 })
        {
            return false;
        }

        path = new(parts[0], parts[1]);
        return true;
    }

    public override string ToString() => $"{Part}.{Field}";
}

/// <summary>
/// One row of a Copy fields / Move fields mapping: source field, target field, and the
/// per-field required marker of the field-dependency ruling (docs/workflows.md › Posting on
/// workflows). Required and empty on the source fails the activity with the field named;
/// optional and empty is skipped - "use it if you have it".
/// </summary>
public sealed record FieldMapping(string From, string To, bool Required = true);

public sealed record FieldCopyOutcome(string From, string To, bool Copied, string? Skipped);

/// <summary>The copy as a whole: a failure names the row that stopped it; outcomes list every row reached.</summary>
public sealed record FieldCopyResult(string? Failure, IReadOnlyList<FieldCopyOutcome> Outcomes)
{
    public bool Succeeded => Failure is null;
    public int Copied => Outcomes.Count(o => o.Copied);
}

/// <summary>
/// Copies field values between content items by type, never by name alone: both sides are
/// resolved against the tenant's current definitions (they may have changed since publish),
/// a pair of the same field type copies the field's JSON whole (text, numeric, date, option
/// picker by selected ids, content picker by ids, ...), text and numeric convert into each
/// other, everything else is refused as incompatible. Move clears the source field after
/// copying. Nothing in here knows what the items are.
/// </summary>
public sealed class ContentFieldValueCopier(IContentDefinitionManager definitions, ITypeActivatorFactory<ContentField> fieldActivators)
{
    private const string TextFieldType = "TextField";
    private const string NumericFieldType = "NumericField";

    public async Task<FieldCopyResult> CopyAsync(ContentItem source, ContentItem target, IReadOnlyList<FieldMapping> mappings, bool move)
    {
        var sourceType = await definitions.GetTypeDefinitionAsync(source.ContentType);
        var targetType = await definitions.GetTypeDefinitionAsync(target.ContentType);
        if (sourceType is null || targetType is null)
        {
            return new($"Unknown content type '{(sourceType is null ? source.ContentType : target.ContentType)}'.", []);
        }

        var outcomes = new List<FieldCopyOutcome>();
        foreach (var mapping in mappings)
        {
            if (!FieldPath.TryParse(mapping.From, out var from) || !FieldPath.TryParse(mapping.To, out var to))
            {
                return new($"A mapping must read Part.Field → Part.Field; got '{mapping.From}' → '{mapping.To}'.", outcomes);
            }

            var sourceField = Resolve(sourceType, from);
            if (sourceField is null)
            {
                if (mapping.Required)
                {
                    return new($"Required field {from} does not exist on {source.ContentType}.", outcomes);
                }

                outcomes.Add(new(from.ToString(), to.ToString(), false, $"{from} does not exist on {source.ContentType}"));
                continue;
            }

            var targetField = Resolve(targetType, to);
            if (targetField is null)
            {
                return new($"Target field {to} does not exist on {target.ContentType}.", outcomes);
            }

            var value = Read(source, from);
            if (IsEmpty(sourceField.FieldDefinition.Name, value))
            {
                if (mapping.Required)
                {
                    return new($"Required field {from} is empty on {source.ContentType} '{source.ContentItemId}'.", outcomes);
                }

                outcomes.Add(new(from.ToString(), to.ToString(), false, $"{from} is empty"));
                continue;
            }

            var converted = Convert(sourceField.FieldDefinition.Name, targetField.FieldDefinition.Name, value!);
            if (converted is null)
            {
                return new($"{from} ({sourceField.FieldDefinition.Name}) cannot be copied into {to} ({targetField.FieldDefinition.Name}).", outcomes);
            }

            Write(target, to, targetField.FieldDefinition.Name, converted);
            if (move)
            {
                Write(source, from, sourceField.FieldDefinition.Name, null);
            }

            outcomes.Add(new(from.ToString(), to.ToString(), true, null));
        }

        return new(null, outcomes);
    }

    /// <summary>The field definition a path names on a type, or null.</summary>
    public static ContentPartFieldDefinition? Resolve(ContentTypeDefinition type, FieldPath path)
    {
        var part = type.Parts.FirstOrDefault(p => string.Equals(p.Name, path.Part, StringComparison.OrdinalIgnoreCase));
        return part?.PartDefinition.Fields.FirstOrDefault(f => string.Equals(f.Name, path.Field, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The field's JSON node on an item, or null when the part or field is absent.</summary>
    public static JsonObject? Read(ContentItem item, FieldPath path)
    {
        JsonObject root = (JsonDynamicObject)item.Content;
        return root[path.Part] is JsonObject part && part[path.Field] is JsonObject field ? field : null;
    }

    /// <summary>
    /// Writes the field's JSON (null clears it) and re-applies a typed field element over it:
    /// a field element Crest has already handed out keeps the values it was deserialized
    /// with, not the node's, so the node alone would leave the item's own reader (the service
    /// building its response) seeing the old value. Apply replaces the cached element and
    /// drops the item's cached parts.
    /// </summary>
    private void Write(ContentItem item, FieldPath path, string fieldType, JsonObject? value)
    {
        var type = fieldActivators.GetTypeActivator(fieldType).Type;
        var element = value is null
            ? (ContentElement)Activator.CreateInstance(type)!
            : (ContentElement)value.Deserialize(type, JOptions.Default)!;

        var part = item.GetOrCreate<ContentPart>(path.Part);
        JsonObject partData = (JsonDynamicObject)part.Content;
        partData[path.Field] = value?.DeepClone() ?? new JsonObject();
        part.Apply(path.Field, element);
    }

    /// <summary>A field with no value: the known shapes by their value property, anything else by having only nulls.</summary>
    public static bool IsEmpty(string fieldType, JsonObject? field)
    {
        if (field is null)
        {
            return true;
        }

        return fieldType switch
        {
            TextFieldType => string.IsNullOrEmpty(field["Text"]?.GetValue<string>()),
            "OptionPickerField" => field["SelectedIds"] is not JsonArray { Count: > 0 },
            "ContentPickerField" => field["ContentItemIds"] is not JsonArray { Count: > 0 },
            "UserPickerField" => field["UserIds"] is not JsonArray { Count: > 0 },
            "TaxonomyField" => field["TermContentItemIds"] is not JsonArray { Count: > 0 },
            _ => field.All(p => p.Value is null || (p.Value is JsonArray array && array.Count == 0)),
        };
    }

    private static JsonObject? Convert(string fromType, string toType, JsonObject value)
    {
        if (string.Equals(fromType, toType, StringComparison.Ordinal))
        {
            return value;
        }

        if (fromType == NumericFieldType && toType == TextFieldType)
        {
            var number = value["Value"]?.GetValue<decimal>();
            return new JsonObject { ["Text"] = number?.ToString(CultureInfo.InvariantCulture) };
        }

        if (fromType == TextFieldType && toType == NumericFieldType)
        {
            var text = value["Text"]?.GetValue<string>();
            return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? new JsonObject { ["Value"] = number } : null;
        }

        return null;
    }

    public static IReadOnlyList<FieldMapping> ParseMappings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<FieldMapping>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
    }
}
