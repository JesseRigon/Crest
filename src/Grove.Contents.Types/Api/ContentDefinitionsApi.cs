#nullable enable

using System.Text.Json;
using System.Text.Json.Nodes;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Contents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Grove.Contents.Types.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Grove.Api;
using Grove.Validation;

namespace Grove.Contents.Types.Api;

public sealed class ContentDefinitionsApi : ApiContributor
{
    public override string Name => "ContentDefinitions";

    public override string FeatureId => "Grove.Contents.Types";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("content-types");
        routes.MapPost("", (HttpContext __http, [FromBody] CreateContentTypeRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).CreateTypeAsync(request));
        routes.MapDelete("{contentType}", (HttpContext __http, [FromRoute] string contentType) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).DeleteTypeAsync(contentType));
        routes.MapGet("{contentType}/settings", (HttpContext __http, [FromRoute] string contentType) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).TypeSettingsAsync(contentType));
        routes.MapPut("{contentType}/settings", (HttpContext __http, [FromRoute] string contentType, [FromBody] DefinitionSettingsRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).SetTypeSettingsAsync(contentType, request));
        routes.MapPut("{contentType}/field-sets/{fieldSetName}/settings", (HttpContext __http, [FromRoute] string contentType, [FromRoute] string fieldSetName, [FromBody] DefinitionSettingsRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).SetTypeFieldSetSettingsAsync(contentType, fieldSetName, request));
        routes.MapPut("{contentType}/field-sets/order", (HttpContext __http, [FromRoute] string contentType, [FromBody] DefinitionOrderRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).OrderTypeFieldSetsAsync(contentType, request));
        routes.MapPost("field-sets", (HttpContext __http, [FromBody] CreateContentFieldSetRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).CreateFieldSetAsync(request));
        routes.MapDelete("field-sets/{fieldSetName}", (HttpContext __http, [FromRoute] string fieldSetName) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).DeleteFieldSetAsync(fieldSetName));
        routes.MapGet("field-sets/{fieldSetName}/settings", (HttpContext __http, [FromRoute] string fieldSetName) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).FieldSetSettingsAsync(fieldSetName));
        routes.MapPut("field-sets/{fieldSetName}/settings", (HttpContext __http, [FromRoute] string fieldSetName, [FromBody] DefinitionSettingsRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).SetFieldSetSettingsAsync(fieldSetName, request));
        routes.MapPut("field-sets/{fieldSetName}/fields/{fieldName}/settings", (HttpContext __http, [FromRoute] string fieldSetName, [FromRoute] string fieldName, [FromBody] DefinitionSettingsRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).SetFieldSettingsAsync(fieldSetName, fieldName, request));
        routes.MapPut("field-sets/{fieldSetName}/fields/{fieldName}/display-name", (HttpContext __http, [FromRoute] string fieldSetName, [FromRoute] string fieldName, [FromBody] FieldDisplayNameRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).RenameFieldAsync(fieldSetName, fieldName, request));
        routes.MapDelete("field-sets/{fieldSetName}/fields/{fieldName}", (HttpContext __http, [FromRoute] string fieldSetName, [FromRoute] string fieldName) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).RemoveFieldAsync(fieldSetName, fieldName));
        routes.MapPut("field-sets/{fieldSetName}/fields/order", (HttpContext __http, [FromRoute] string fieldSetName, [FromBody] DefinitionOrderRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).OrderFieldsAsync(fieldSetName, request));
    }
}


public sealed record CreateContentTypeRequest([property: NotEmpty, Matches("^[A-Za-z][A-Za-z0-9_]*$", ErrorMessage = "A type name starts with a letter and holds letters, digits and underscores only.")] string Name, string? DisplayName, string? Stereotype = null);

public sealed record CreateContentFieldSetRequest([property: NotEmpty, Matches("^[A-Za-z][A-Za-z0-9_]*$", ErrorMessage = "A part name starts with a letter and holds letters, digits and underscores only.")] string Name, string? DisplayName = null, string? Description = null);

/// <summary>A definition's settings as stored: every section, keyed by settings type name.</summary>
public sealed record DefinitionSettingsRequest(JsonObject? Settings, string? DisplayName = null);

public sealed record DefinitionOrderRequest(IReadOnlyList<string> Names);

public sealed record FieldDisplayNameRequest([property: NotEmpty] string DisplayName);

public sealed record TypeFieldSetSettingsModel(string Name, string FieldSetName, JsonObject Settings);

public sealed record ContentTypeSettingsModel(string Name, string DisplayName, JsonObject Settings, IReadOnlyList<TypeFieldSetSettingsModel> FieldSets);

public sealed record FieldSettingsModel(string Name, string FieldType, JsonObject Settings);

public sealed record ContentFieldSetSettingsModel(string Name, JsonObject Settings, IReadOnlyList<FieldSettingsModel> Fields, IReadOnlyList<string> UsedByTypes);

/// <summary>
/// The content definition editor's writes (Content types and Field groups pages, step 7 of
/// docs/blazor-display.md, replacing the MVC ContentTypes admin): create and delete types and parts,
/// remove, rename and reorder fields and parts, and every settings section of a type, a part on a
/// type, a part and a field - settings edited as their stored JSON, so a section a module adds
/// needs no editor. Definition locks hold on every path (docs/content-engine.md › Definition locks).
/// </summary>
public sealed class ContentDefinitionsHandlers(
    IContentDefinitionManager definitions,
    IOptions<ContentOptions> contentOptions,
    IAuthorizationService authorization,
    DefinitionLockGuard locks) : ApiHandlers
{
    public async Task<IResult> CreateTypeAsync([FromBody] CreateContentTypeRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadTypeDefinitionAsync(request.Name) is not null) return Problem($"A content type named '{request.Name}' exists.", statusCode: StatusCodes.Status409Conflict);
        if (await definitions.LoadFieldSetDefinitionAsync(request.Name) is not null) return Problem($"A field set named '{request.Name}' exists; a type's own part takes its name.", statusCode: StatusCodes.Status409Conflict);

        // As the stock admin does: the type and its own part, named alike.
        await definitions.AlterFieldSetDefinitionAsync(request.Name, part => { });
        await definitions.AlterTypeDefinitionAsync(request.Name, type =>
        {
            type.DisplayedAs(string.IsNullOrWhiteSpace(request.DisplayName) ? request.Name : request.DisplayName.Trim());
            if (!string.IsNullOrWhiteSpace(request.Stereotype))
            {
                type.Stereotype(request.Stereotype.Trim());
            }

            type.WithFieldSet(request.Name, part => part.WithPosition("0"));
        });

        return Created($"api/grove/content-types/{request.Name}", null);
    }

    public async Task<IResult> DeleteTypeAsync(string contentType)
    {
        if (!await CanEditAsync()) return Forbid();
        var type = await definitions.LoadTypeDefinitionAsync(contentType);
        if (type is null) return NotFound();

        await locks.EnsureTypeDeleteAsync(contentType);
        await definitions.DeleteTypeDefinitionAsync(contentType);

        // The type's own field set goes with it unless code defines it or another type attaches
        // it. Stored items stay in their documents; they no longer have a definition to be
        // edited through.
        if (await TypeOwnFieldSet.CanDeleteAsync(definitions, contentOptions.Value, contentType) &&
            await definitions.LoadFieldSetDefinitionAsync(contentType) is not null)
        {
            await definitions.DeleteFieldSetDefinitionAsync(contentType);
        }

        return NoContent();
    }

    public async Task<IResult> TypeSettingsAsync(string contentType)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();
        var type = await definitions.LoadTypeDefinitionAsync(contentType);
        if (type is null) return NotFound();
        return Ok(new ContentTypeSettingsModel(
            type.Name,
            type.DisplayName,
            Clone(type.Settings),
            [.. type.FieldSets.Select(part => new TypeFieldSetSettingsModel(part.Name, part.FieldSetDefinition.Name, Clone(part.Settings)))]));
    }

    public async Task<IResult> SetTypeSettingsAsync(string contentType, [FromBody] DefinitionSettingsRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadTypeDefinitionAsync(contentType) is not { } definition) return NotFound();
        if (request.Settings is not null)
        {
            // The type's field rules are settings too: the same locks apply as through their own editor.
            var requested = request.Settings[nameof(FieldRulesSettings)]?.Deserialize<FieldRulesSettings>()?.Rules ?? [];
            await locks.EnsureFieldRulesChangeAsync(contentType, definition.GetSettings<FieldRulesSettings>()?.Rules ?? [], requested);
        }

        await definitions.AlterTypeDefinitionAsync(contentType, type =>
        {
            if (!string.IsNullOrWhiteSpace(request.DisplayName))
            {
                type.DisplayedAs(request.DisplayName.Trim());
            }

            if (request.Settings is not null)
            {
                type.ReplaceSettings(request.Settings);
            }
        });
        return NoContent();
    }

    public async Task<IResult> SetTypeFieldSetSettingsAsync(string contentType, string fieldSetName, [FromBody] DefinitionSettingsRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var type = await definitions.LoadTypeDefinitionAsync(contentType);
        if (type?.FieldSets.Any(part => string.Equals(part.Name, fieldSetName, StringComparison.OrdinalIgnoreCase)) != true) return NotFound();
        if (request.Settings is null) return Invalid("Settings are required.");

        await locks.EnsureTypeFieldSetChangeAsync(contentType, fieldSetName, DefinitionChanges.SettingsChanged);
        await definitions.AlterTypeDefinitionAsync(contentType, builder => builder.WithFieldSet(fieldSetName, part => part.ReplaceSettings(request.Settings)));
        return NoContent();
    }

    /// <summary>Orders the type's parts: each named part takes its index as its position.</summary>
    public async Task<IResult> OrderTypeFieldSetsAsync(string contentType, [FromBody] DefinitionOrderRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var type = await definitions.LoadTypeDefinitionAsync(contentType);
        if (type is null) return NotFound();
        var names = request.Names ?? [];
        if (names.Any(name => type.FieldSets.All(part => !string.Equals(part.Name, name, StringComparison.OrdinalIgnoreCase)))) return Invalid("Every name must be a part on the type.");

        await definitions.AlterTypeDefinitionAsync(contentType, builder =>
        {
            for (var index = 0; index < names.Count; index++)
            {
                var position = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                builder.WithFieldSet(names[index], part => part.WithPosition(position));
            }
        });
        return NoContent();
    }

    public async Task<IResult> CreateFieldSetAsync([FromBody] CreateContentFieldSetRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadFieldSetDefinitionAsync(request.Name) is not null) return Problem($"A field set named '{request.Name}' exists.", statusCode: StatusCodes.Status409Conflict);

        await definitions.AlterFieldSetDefinitionAsync(request.Name, part =>
        {
            part.Attachable();
            if (!string.IsNullOrWhiteSpace(request.DisplayName)) part.WithDisplayName(request.DisplayName.Trim());
            if (!string.IsNullOrWhiteSpace(request.Description)) part.WithDescription(request.Description.Trim());
        });
        return Created($"api/grove/content-types/field-sets/{request.Name}", null);
    }

    public async Task<IResult> DeleteFieldSetAsync(string fieldSetName)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadFieldSetDefinitionAsync(fieldSetName) is null) return NotFound();
        var usedBy = (await definitions.LoadTypeDefinitionsAsync()).Where(type => type.FieldSets.Any(part => part.FieldSetDefinition.Name == fieldSetName)).Select(type => type.Name).ToArray();
        if (usedBy.Length > 0) return Problem($"The part is on {string.Join(", ", usedBy)}; detach it first.", statusCode: StatusCodes.Status409Conflict);

        await locks.EnsureFieldSetChangeAsync(fieldSetName, DefinitionChanges.Deleted);
        await definitions.DeleteFieldSetDefinitionAsync(fieldSetName);
        return NoContent();
    }

    public async Task<IResult> FieldSetSettingsAsync(string fieldSetName)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();
        var part = await definitions.LoadFieldSetDefinitionAsync(fieldSetName);
        if (part is null) return NotFound();
        var usedBy = (await definitions.LoadTypeDefinitionsAsync()).Where(type => type.FieldSets.Any(candidate => candidate.FieldSetDefinition.Name == fieldSetName)).Select(type => type.Name).ToArray();
        return Ok(new ContentFieldSetSettingsModel(
            part.Name,
            Clone(part.Settings),
            [.. part.Fields.Select(field => new FieldSettingsModel(field.Name, field.FieldTypeDefinition.Name, Clone(field.Settings)))],
            usedBy));
    }

    public async Task<IResult> SetFieldSetSettingsAsync(string fieldSetName, [FromBody] DefinitionSettingsRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadFieldSetDefinitionAsync(fieldSetName) is null) return NotFound();
        if (request.Settings is null) return Invalid("Settings are required.");

        await locks.EnsureFieldSetChangeAsync(fieldSetName, DefinitionChanges.SettingsChanged);
        await definitions.AlterFieldSetDefinitionAsync(fieldSetName, part => part.ReplaceSettings(request.Settings));
        return NoContent();
    }

    public async Task<IResult> SetFieldSettingsAsync(string fieldSetName, string fieldName, [FromBody] DefinitionSettingsRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var field = (await definitions.LoadFieldSetDefinitionAsync(fieldSetName))?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null) return NotFound();
        if (request.Settings is null) return Invalid("Settings are required.");

        await locks.EnsureFieldChangeAsync(fieldSetName, fieldName, DefinitionChanges.SettingsChanged);
        await definitions.AlterFieldSetDefinitionAsync(fieldSetName, part => part.WithField(field.Name, builder => builder.ReplaceSettings(request.Settings)));
        return NoContent();
    }

    /// <summary>Renames a field for people: its display name. The technical name is the stored content's key and does not change.</summary>
    public async Task<IResult> RenameFieldAsync(string fieldSetName, string fieldName, [FromBody] FieldDisplayNameRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var field = (await definitions.LoadFieldSetDefinitionAsync(fieldSetName))?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null) return NotFound();

        await definitions.AlterFieldSetDefinitionAsync(fieldSetName, part => part.WithField(field.Name, builder => builder.WithDisplayName(request.DisplayName.Trim())));
        return NoContent();
    }

    public async Task<IResult> RemoveFieldAsync(string fieldSetName, string fieldName)
    {
        if (!await CanEditAsync()) return Forbid();
        var field = (await definitions.LoadFieldSetDefinitionAsync(fieldSetName))?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null) return NotFound();

        await locks.EnsureFieldChangeAsync(fieldSetName, fieldName, DefinitionChanges.Removed);
        await definitions.AlterFieldSetDefinitionAsync(fieldSetName, part => part.RemoveField(field.Name));
        return NoContent();
    }

    /// <summary>Orders a part's fields: each named field takes its index as its position.</summary>
    public async Task<IResult> OrderFieldsAsync(string fieldSetName, [FromBody] DefinitionOrderRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var part = await definitions.LoadFieldSetDefinitionAsync(fieldSetName);
        if (part is null) return NotFound();
        var names = request.Names ?? [];
        if (names.Any(name => part.Fields.All(field => !string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)))) return Invalid("Every name must be a field of the part.");

        await definitions.AlterFieldSetDefinitionAsync(fieldSetName, builder =>
        {
            for (var index = 0; index < names.Count; index++)
            {
                var position = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                builder.WithField(names[index], field => field.WithPosition(position));
            }
        });
        return NoContent();
    }

    private Task<bool> CanEditAsync() => authorization.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes);

    private static IResult Invalid(string message) => Problem(message, statusCode: StatusCodes.Status400BadRequest);


    private static JsonObject Clone(JsonObject? settings) => settings?.DeepClone() as JsonObject ?? [];
}
