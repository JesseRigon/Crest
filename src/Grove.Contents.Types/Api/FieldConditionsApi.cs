#nullable enable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Validation;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Display.Management.Components;

namespace Grove.Contents.Types.Api;

public sealed class FieldConditionsApi : ApiContributor
{
    public override string Name => "FieldConditions";

    public override string FeatureId => "Grove.Contents.Types";

    public override void Map(IEndpointRouteBuilder group)
    {
        var visibility = group.MapGroup("field-visibility");
        visibility.MapGet("", (HttpContext __http, [FromQuery] string fieldSet, [FromQuery] string field) => ApiHandlers.For<FieldConditionsHandlers>(__http).GetVisibilityAsync(fieldSet, field));
        visibility.MapPut("", (HttpContext __http, [FromBody] FieldVisibilityRequest request) => ApiHandlers.For<FieldConditionsHandlers>(__http).PutVisibilityAsync(request));

        var rules = group.MapGroup("content-types/{contentType}/field-rules");
        rules.MapGet("", (HttpContext __http, [FromRoute] string contentType) => ApiHandlers.For<FieldConditionsHandlers>(__http).GetRulesAsync(contentType));
        rules.MapPut("", (HttpContext __http, [FromRoute] string contentType, [FromBody] FieldRulesRequest request) => ApiHandlers.For<FieldConditionsHandlers>(__http).PutRulesAsync(contentType, request));
    }
}

/// <summary>
/// Reads and writes a type's field conditions (docs/content-engine.md › Validation and conditions
/// run on the one expression engine): a field's visibility condition
/// (<see cref="FieldVisibilitySettings"/>, on the field set's field definition, so it applies on
/// every type carrying the field set) and a type's field rules (<see cref="FieldRulesSettings"/>).
/// Definition work, gated like the other definition editors and refused on locked fields.
/// </summary>
public sealed class FieldConditionsHandlers(
    IContentDefinitionManager contentDefinitionManager,
    IAuthorizationService authorization,
    DefinitionLockGuard locks) : ApiHandlers
{
    public async Task<IResult> GetVisibilityAsync([FromQuery] string fieldSet, [FromQuery] string field)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();

        var definition = await FindFieldAsync(fieldSet, field);
        return definition is null
            ? Ok(new FieldVisibilityModel(false, new FieldVisibilitySettings()))
            : Ok(new FieldVisibilityModel(true, definition.GetSettings<FieldVisibilitySettings>() ?? new FieldVisibilitySettings()));
    }

    /// <summary>Replaces the condition wholesale; no condition makes the field always visible.</summary>
    public async Task<IResult> PutVisibilityAsync([FromBody] FieldVisibilityRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var fieldSet = await contentDefinitionManager.GetFieldSetDefinitionAsync(request.FieldSet);
        var definition = fieldSet?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, request.Field, StringComparison.OrdinalIgnoreCase));
        if (definition is null)
        {
            return NotFound();
        }

        // A locked field cannot be made conditional (hidden); 409 via the lock filter.
        await locks.EnsureFieldChangeAsync(request.FieldSet, request.Field, DefinitionChanges.SettingsChanged);

        var when = request.Settings?.When;
        if (when is not null)
        {
            // The field set is shared, so its conditions name their siblings relative to it: a bare
            // field name, read from the same attachment wherever the field set is attached.
            var problems = FieldConditions.Check(when, path => !path.Contains('.')
                && fieldSet!.Fields.Any(candidate => string.Equals(candidate.Name, path, StringComparison.OrdinalIgnoreCase)));
            if (problems.Count > 0)
            {
                return Problem(string.Join(" ", problems), statusCode: StatusCodes.Status400BadRequest);
            }
        }

        await contentDefinitionManager.AlterFieldSetDefinitionAsync(request.FieldSet, builder => builder
            .WithField(request.Field, field => field
                .MergeSettings<FieldVisibilitySettings>(settings => settings.When = when)));

        return NoContent();
    }

    public async Task<IResult> GetRulesAsync(string contentType)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();

        var type = await contentDefinitionManager.GetTypeDefinitionAsync(contentType);
        return type is null ? NotFound() : Ok(type.GetSettings<FieldRulesSettings>() ?? new FieldRulesSettings());
    }

    /// <summary>Replaces the type's rules wholesale. A rule on a locked field cannot be added, changed or removed (409).</summary>
    public async Task<IResult> PutRulesAsync(string contentType, [FromBody] FieldRulesRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var type = await contentDefinitionManager.LoadTypeDefinitionAsync(contentType);
        if (type is null)
        {
            return NotFound();
        }

        var rules = request.Rules ?? [];
        var problems = new List<string>();
        foreach (var (rule, index) in rules.Select((rule, index) => (rule, index)))
        {
            if (rule.When is null)
            {
                problems.Add($"Rule {index + 1} has no condition.");
            }
            else
            {
                problems.AddRange(FieldConditions.Check(rule.When, path => FieldExists(type, path)).Select(problem => $"Rule {index + 1}: {problem}"));
            }

            if (!FieldExists(type, rule.Field))
            {
                problems.Add($"Rule {index + 1}: '{rule.Field}' is not a field (FieldSet.Field) of the type.");
            }

            if (!FieldRuleEffects.All.Contains(rule.Effect, StringComparer.OrdinalIgnoreCase))
            {
                problems.Add($"Rule {index + 1}: '{rule.Effect}' is not one of {string.Join(", ", FieldRuleEffects.All)}.");
            }
            else if (string.Equals(rule.Effect, FieldRuleEffects.Refuse, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(rule.Message))
            {
                problems.Add($"Rule {index + 1}: a refusal needs its message.");
            }
        }

        if (problems.Count > 0)
        {
            return Problem(string.Join(" ", problems), statusCode: StatusCodes.Status400BadRequest);
        }

        var current = type.GetSettings<FieldRulesSettings>()?.Rules ?? [];
        await locks.EnsureFieldRulesChangeAsync(contentType, current, rules);

        await contentDefinitionManager.AlterTypeDefinitionAsync(contentType, builder => builder
            .MergeSettings<FieldRulesSettings>(settings => settings.Rules =
            [
                .. rules.Select(rule => new FieldRule
                {
                    When = rule.When,
                    Field = rule.Field.Trim(),
                    Effect = rule.Effect.ToLowerInvariant(),
                    Message = string.IsNullOrWhiteSpace(rule.Message) ? null : rule.Message.Trim(),
                }),
            ]));

        return NoContent();
    }

    private async Task<ContentFieldDefinition?> FindFieldAsync(string fieldSet, string field)
    {
        var definition = await contentDefinitionManager.GetFieldSetDefinitionAsync(fieldSet);
        return definition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, field, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether <paramref name="path"/> (<c>FieldSet.Field</c>) names a field of the type, by attachment name.</summary>
    public static bool FieldExists(ContentTypeDefinition type, string? path)
    {
        var segments = path?.Split('.') ?? [];
        return segments.Length == 2 && type.FieldSets.Any(fieldSet =>
            string.Equals(fieldSet.Name, segments[0], StringComparison.OrdinalIgnoreCase)
            && fieldSet.FieldSetDefinition?.Fields.Any(field => string.Equals(field.Name, segments[1], StringComparison.OrdinalIgnoreCase)) == true);
    }
}

/// <summary>FieldExists distinguishes "no condition yet" from "no such field".</summary>
public sealed record FieldVisibilityModel(bool FieldExists, FieldVisibilitySettings Settings);

public sealed record FieldVisibilityRequest([property: NotEmpty] string FieldSet, [property: NotEmpty] string Field, FieldVisibilitySettings? Settings);

public sealed record FieldRulesRequest(List<FieldRule>? Rules);
