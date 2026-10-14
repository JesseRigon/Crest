#nullable enable

using Crest.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Crest.Api;
using Crest.Validation;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.Contents;

namespace Crest.ContentTypes.Api;

public sealed class FieldVisibilityApi : ApiContributor
{
    public override string Name => "FieldVisibility";

    public override string FeatureId => "Crest.ContentTypes";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("field-visibility");
        routes.MapGet("", (HttpContext __http, [FromQuery] string part, [FromQuery] string field) => ApiHandlers.For<FieldVisibilityHandlers>(__http).GetAsync(part, field));
        routes.MapPut("", (HttpContext __http, [FromBody] FieldVisibilityRequest request) => ApiHandlers.For<FieldVisibilityHandlers>(__http).PutAsync(request));
    }
}


/// <summary>
/// Reads and writes a field's <see cref="CrestFieldVisibilitySettings"/> — the
/// declarative show/hide condition any field type can carry. Definition work,
/// gated exactly like the other definition editors; the settings live on the
/// PART's field definition, so a condition applies on every type carrying the
/// part (same sharing rule as the field itself).
/// </summary>
public sealed class FieldVisibilityHandlers(
    IContentDefinitionManager contentDefinitionManager,
    IAuthorizationService authorization,
    DefinitionLockGuard locks) : ApiHandlers
{
    public async Task<IResult> GetAsync([FromQuery] string part, [FromQuery] string field)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();

        var definition = await FindFieldAsync(part, field);
        return definition is null
            ? Ok(new FieldVisibilityModel(false, new CrestFieldVisibilitySettings()))
            : Ok(new FieldVisibilityModel(true, definition.GetSettings<CrestFieldVisibilitySettings>() ?? new CrestFieldVisibilitySettings()));
    }

    /// <summary>Replaces the condition wholesale (a blank Path clears it) — a JSON
    /// merge would resurrect removed keys.</summary>
    public async Task<IResult> PutAsync([FromBody] FieldVisibilityRequest request)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes)) return Forbid();

        var definition = await FindFieldAsync(request.Part, request.Field);
        if (definition is null)
        {
            return NotFound();
        }

        // A locked field cannot be made conditional (hidden); 409 via the lock filter.
        await locks.EnsureFieldChangeAsync(request.Part, request.Field, DefinitionChanges.SettingsChanged);

        var incoming = request.Settings ?? new CrestFieldVisibilitySettings();
        var hasCondition = !string.IsNullOrWhiteSpace(incoming.Path);
        if (hasCondition && !FieldVisibilityOperators.All.Contains(incoming.Operator, StringComparer.OrdinalIgnoreCase))
        {
            return Problem($"'{incoming.Operator}' is not a visibility operator.", statusCode: StatusCodes.Status400BadRequest);
        }

        await contentDefinitionManager.AlterPartDefinitionAsync(request.Part, part => part
            .WithField(request.Field, field => field
                .MergeSettings<CrestFieldVisibilitySettings>(settings =>
                {
                    settings.Path = hasCondition ? incoming.Path!.Trim() : null;
                    settings.Operator = hasCondition ? incoming.Operator : FieldVisibilityOperators.Any;
                    settings.Keys = hasCondition
                        ? [.. (incoming.Keys ?? []).Where(key => !string.IsNullOrWhiteSpace(key)).Select(key => key.Trim())]
                        : [];
                })));

        return NoContent();
    }

    private async Task<ContentPartFieldDefinition?> FindFieldAsync(string part, string field)
    {
        var definition = await contentDefinitionManager.GetPartDefinitionAsync(part);
        return definition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, field, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>FieldExists distinguishes "no condition yet" from "no such field".</summary>
public sealed record FieldVisibilityModel(bool FieldExists, CrestFieldVisibilitySettings Settings);

public sealed record FieldVisibilityRequest([property: NotEmpty] string Part, [property: NotEmpty] string Field, CrestFieldVisibilitySettings? Settings);
