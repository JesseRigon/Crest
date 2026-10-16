#nullable enable

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Dynamic;
using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Handlers;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Components;
using Microsoft.Extensions.Localization;

namespace Grove.Contents.Types;

/// <summary>
/// The save side of a type's field conditions (<see cref="IContentFieldConditions"/>): a field
/// whose visibility condition (<see cref="FieldVisibilitySettings"/>) is false has its value
/// cleared, and the type's field rules (<see cref="FieldRulesSettings"/>) are evaluated. Both use
/// the evaluator the editor uses (<see cref="FieldConditions"/>), over the fields' comparison
/// values (<see cref="IFieldComparisonValueResolver"/>).
/// </summary>
public sealed class FieldConditionEnforcer(
    IContentDefinitionManager contentDefinitionManager,
    IEnumerable<IFieldComparisonValueResolver> comparisonValues,
    IStringLocalizer<FieldConditionEnforcer> S) : IContentFieldConditions
{
    public async Task ClearHiddenFieldsAsync(ContentItem contentItem, CancellationToken cancellationToken = default)
    {
        var type = await contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
        var conditioned = Conditioned(type);
        if (conditioned.Count == 0 || Content(contentItem) is not { } content)
        {
            return;
        }

        // Fixpoint: clearing one field can turn another field's condition false in the same save
        // (A visible when B has a value, B visible when C has one). Bounded by the number of
        // conditioned fields: a longer chain is a cycle, whose remaining members keep their values.
        for (var pass = 0; pass < conditioned.Count; pass++)
        {
            var clearedAny = false;
            foreach (var (fieldSetName, fieldName, when) in conditioned)
            {
                if (content[fieldSetName]?[fieldName] is null
                    || await IsMetAsync(contentItem.ContentType, content, when, cancellationToken))
                {
                    continue;
                }

                // Mutate the root document directly, the write style the save path itself uses: an
                // element served by Alter<ContentFieldSet> may be cached from before the save
                // replaced the field set's subtree, and its merge would bring the value back.
                if (content[fieldSetName] is JsonObject fieldSet && fieldSet.Remove(fieldName))
                {
                    clearedAny = true;
                }
            }

            if (!clearedAny)
            {
                return;
            }
        }
    }

    public async Task ValidateRulesAsync(ContentItem contentItem, ContentValidateResult result, CancellationToken cancellationToken = default)
    {
        var type = await contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);
        var rules = type?.GetSettings<FieldRulesSettings>()?.Rules ?? [];
        if (rules.Count == 0 || Content(contentItem) is not { } content)
        {
            return;
        }

        var visibility = Conditioned(type).ToDictionary(entry => $"{entry.FieldSetName}.{entry.FieldName}", entry => entry.When, StringComparer.OrdinalIgnoreCase);
        foreach (var rule in rules)
        {
            if (rule.When is null || string.IsNullOrWhiteSpace(rule.Field)
                || !await IsMetAsync(contentItem.ContentType, content, rule.When, cancellationToken))
            {
                continue;
            }

            if (string.Equals(rule.Effect, FieldRuleEffects.Refuse, StringComparison.OrdinalIgnoreCase))
            {
                result.Fail(new ValidationResult(string.IsNullOrWhiteSpace(rule.Message) ? S["{0} is not allowed here.", rule.Field].Value : rule.Message, [rule.Field]));
                continue;
            }

            // Required: unless its own visibility hides the field (a hidden field holds no value).
            if (FieldConditions.StoredValues(content, rule.Field).Count > 0
                || (visibility.TryGetValue(rule.Field, out var when) && !await IsMetAsync(contentItem.ContentType, content, when, cancellationToken)))
            {
                continue;
            }

            result.Fail(new ValidationResult(string.IsNullOrWhiteSpace(rule.Message) ? S["A value is required for {0}.", Label(type!, rule.Field)].Value : rule.Message, [rule.Field]));
        }
    }

    /// <summary>The fields of <paramref name="type"/> carrying a visibility condition, by attachment name.</summary>
    private static List<(string FieldSetName, string FieldName, DisplayBinding When)> Conditioned(ContentTypeDefinition? type)
    {
        var conditioned = new List<(string, string, DisplayBinding)>();
        foreach (var typeFieldSet in type?.FieldSets ?? [])
        {
            foreach (var field in typeFieldSet.FieldSetDefinition?.Fields ?? [])
            {
                // A visibility condition names its siblings relative to the field set; under this
                // attachment they are this attachment's fields.
                if (field.GetSettings<FieldVisibilitySettings>() is { When: { } when })
                {
                    conditioned.Add((typeFieldSet.Name, field.Name, FieldConditions.Qualify(when, typeFieldSet.Name)));
                }
            }
        }

        return conditioned;
    }

    private async Task<bool> IsMetAsync(string contentType, JsonObject content, DisplayBinding condition, CancellationToken cancellationToken)
    {
        // The comparison values are resolved first (an option list field's ids to keys), then the
        // condition is evaluated over them.
        var values = new Dictionary<string, JsonNode?>(StringComparer.OrdinalIgnoreCase);
        var ignoreCase = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolver = comparisonValues.LastOrDefault();
        foreach (var path in FieldConditions.Fields(condition).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var raw = FieldConditions.StoredValues(content, path);
            var resolved = resolver is null
                ? new FieldComparisonValues(raw)
                : await resolver.ResolveAsync(contentType, path, null, raw, cancellationToken);
            values[path] = FieldConditions.Value(resolved.Values);
            if (resolved.IgnoreCase)
            {
                ignoreCase.Add(path);
            }
        }

        if (ignoreCase.Count > 0)
        {
            condition = FieldConditions.IgnoreCase(condition, ignoreCase.Contains);
        }

        return FieldConditions.IsMet(condition, path => values.GetValueOrDefault(path)?.DeepClone());
    }

    private static string Label(ContentTypeDefinition type, string path)
    {
        var segments = path.Split('.');
        var field = type.FieldSets
            .FirstOrDefault(fieldSet => string.Equals(fieldSet.Name, segments[0], StringComparison.OrdinalIgnoreCase))?
            .FieldSetDefinition?.Fields.FirstOrDefault(candidate => segments.Length > 1 && string.Equals(candidate.Name, segments[1], StringComparison.OrdinalIgnoreCase));
        return field?.DisplayName() ?? path;
    }

    private static JsonObject? Content(ContentItem contentItem) =>
        contentItem.Content is JsonDynamicObject dynamicObject ? (JsonObject)dynamicObject : contentItem.Content as JsonObject;
}
