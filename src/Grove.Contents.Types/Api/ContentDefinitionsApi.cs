#nullable enable

using System.Text.Json.Nodes;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents.Management.Metadata.Settings;
using Grove.Contents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
        routes.MapPut("{contentType}/parts/{partName}/settings", (HttpContext __http, [FromRoute] string contentType, [FromRoute] string partName, [FromBody] DefinitionSettingsRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).SetTypePartSettingsAsync(contentType, partName, request));
        routes.MapPut("{contentType}/parts/order", (HttpContext __http, [FromRoute] string contentType, [FromBody] DefinitionOrderRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).OrderTypePartsAsync(contentType, request));
        routes.MapPost("parts", (HttpContext __http, [FromBody] CreateContentPartRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).CreatePartAsync(request));
        routes.MapDelete("parts/{partName}", (HttpContext __http, [FromRoute] string partName) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).DeletePartAsync(partName));
        routes.MapGet("parts/{partName}/settings", (HttpContext __http, [FromRoute] string partName) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).PartSettingsAsync(partName));
        routes.MapPut("parts/{partName}/settings", (HttpContext __http, [FromRoute] string partName, [FromBody] DefinitionSettingsRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).SetPartSettingsAsync(partName, request));
        routes.MapPut("parts/{partName}/fields/{fieldName}/settings", (HttpContext __http, [FromRoute] string partName, [FromRoute] string fieldName, [FromBody] DefinitionSettingsRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).SetFieldSettingsAsync(partName, fieldName, request));
        routes.MapPut("parts/{partName}/fields/{fieldName}/display-name", (HttpContext __http, [FromRoute] string partName, [FromRoute] string fieldName, [FromBody] FieldDisplayNameRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).RenameFieldAsync(partName, fieldName, request));
        routes.MapDelete("parts/{partName}/fields/{fieldName}", (HttpContext __http, [FromRoute] string partName, [FromRoute] string fieldName) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).RemoveFieldAsync(partName, fieldName));
        routes.MapPut("parts/{partName}/fields/order", (HttpContext __http, [FromRoute] string partName, [FromBody] DefinitionOrderRequest request) => ApiHandlers.For<ContentDefinitionsHandlers>(__http).OrderFieldsAsync(partName, request));
    }
}


public sealed record CreateContentTypeRequest([property: NotEmpty, Matches("^[A-Za-z][A-Za-z0-9_]*$", ErrorMessage = "A type name starts with a letter and holds letters, digits and underscores only.")] string Name, string? DisplayName, string? Stereotype = null);

public sealed record CreateContentPartRequest([property: NotEmpty, Matches("^[A-Za-z][A-Za-z0-9_]*$", ErrorMessage = "A part name starts with a letter and holds letters, digits and underscores only.")] string Name, string? DisplayName = null, string? Description = null);

/// <summary>A definition's settings as stored: every section, keyed by settings type name.</summary>
public sealed record DefinitionSettingsRequest(JsonObject? Settings, string? DisplayName = null);

public sealed record DefinitionOrderRequest(IReadOnlyList<string> Names);

public sealed record FieldDisplayNameRequest([property: NotEmpty] string DisplayName);

public sealed record TypePartSettingsModel(string Name, string PartName, JsonObject Settings);

public sealed record ContentTypeSettingsModel(string Name, string DisplayName, JsonObject Settings, IReadOnlyList<TypePartSettingsModel> Parts);

public sealed record PartFieldSettingsModel(string Name, string FieldType, JsonObject Settings);

public sealed record ContentPartSettingsModel(string Name, JsonObject Settings, IReadOnlyList<PartFieldSettingsModel> Fields, IReadOnlyList<string> UsedByTypes);

/// <summary>
/// The content definition editor's writes (Content Types and Content Parts pages, step 7 of
/// docs/blazor-display.md, replacing the MVC ContentTypes admin): create and delete types and parts,
/// remove, rename and reorder fields and parts, and every settings section of a type, a part on a
/// type, a part and a field - settings edited as their stored JSON, so a section a module adds
/// needs no editor. Definition locks hold on every path (docs/content-engine.md › Definition locks).
/// </summary>
public sealed class ContentDefinitionsHandlers(
    IContentDefinitionManager definitions,
    IAuthorizationService authorization,
    DefinitionLockGuard locks) : ApiHandlers
{
    public async Task<IResult> CreateTypeAsync([FromBody] CreateContentTypeRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadTypeDefinitionAsync(request.Name) is not null) return Problem($"A content type named '{request.Name}' exists.", statusCode: StatusCodes.Status409Conflict);
        if (await definitions.LoadPartDefinitionAsync(request.Name) is not null) return Problem($"A content part named '{request.Name}' exists; a type's own part takes its name.", statusCode: StatusCodes.Status409Conflict);

        // As the stock admin does: the type and its own part, named alike.
        await definitions.AlterPartDefinitionAsync(request.Name, part => { });
        await definitions.AlterTypeDefinitionAsync(request.Name, type =>
        {
            type.DisplayedAs(string.IsNullOrWhiteSpace(request.DisplayName) ? request.Name : request.DisplayName.Trim());
            if (!string.IsNullOrWhiteSpace(request.Stereotype))
            {
                type.Stereotype(request.Stereotype.Trim());
            }

            type.WithPart(request.Name, part => part.WithPosition("0"));
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

        // The type's own part goes with it when no other type carries it. Stored items stay in
        // their documents; they no longer have a definition to be edited through.
        var ownPartUsed = (await definitions.LoadTypeDefinitionsAsync()).Any(other => other.Parts.Any(part => part.PartDefinition.Name == contentType));
        if (!ownPartUsed && await definitions.LoadPartDefinitionAsync(contentType) is not null)
        {
            await definitions.DeletePartDefinitionAsync(contentType);
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
            [.. type.Parts.Select(part => new TypePartSettingsModel(part.Name, part.PartDefinition.Name, Clone(part.Settings)))]));
    }

    public async Task<IResult> SetTypeSettingsAsync(string contentType, [FromBody] DefinitionSettingsRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadTypeDefinitionAsync(contentType) is null) return NotFound();
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

    public async Task<IResult> SetTypePartSettingsAsync(string contentType, string partName, [FromBody] DefinitionSettingsRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var type = await definitions.LoadTypeDefinitionAsync(contentType);
        if (type?.Parts.Any(part => string.Equals(part.Name, partName, StringComparison.OrdinalIgnoreCase)) != true) return NotFound();
        if (request.Settings is null) return Invalid("Settings are required.");

        await locks.EnsureTypePartChangeAsync(contentType, partName, DefinitionChanges.SettingsChanged);
        await definitions.AlterTypeDefinitionAsync(contentType, builder => builder.WithPart(partName, part => part.ReplaceSettings(request.Settings)));
        return NoContent();
    }

    /// <summary>Orders the type's parts: each named part takes its index as its position.</summary>
    public async Task<IResult> OrderTypePartsAsync(string contentType, [FromBody] DefinitionOrderRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var type = await definitions.LoadTypeDefinitionAsync(contentType);
        if (type is null) return NotFound();
        var names = request.Names ?? [];
        if (names.Any(name => type.Parts.All(part => !string.Equals(part.Name, name, StringComparison.OrdinalIgnoreCase)))) return Invalid("Every name must be a part on the type.");

        await definitions.AlterTypeDefinitionAsync(contentType, builder =>
        {
            for (var index = 0; index < names.Count; index++)
            {
                var position = index.ToString(System.Globalization.CultureInfo.InvariantCulture);
                builder.WithPart(names[index], part => part.WithPosition(position));
            }
        });
        return NoContent();
    }

    public async Task<IResult> CreatePartAsync([FromBody] CreateContentPartRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadPartDefinitionAsync(request.Name) is not null) return Problem($"A content part named '{request.Name}' exists.", statusCode: StatusCodes.Status409Conflict);

        await definitions.AlterPartDefinitionAsync(request.Name, part =>
        {
            part.Attachable();
            if (!string.IsNullOrWhiteSpace(request.DisplayName)) part.WithDisplayName(request.DisplayName.Trim());
            if (!string.IsNullOrWhiteSpace(request.Description)) part.WithDescription(request.Description.Trim());
        });
        return Created($"api/grove/content-types/parts/{request.Name}", null);
    }

    public async Task<IResult> DeletePartAsync(string partName)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadPartDefinitionAsync(partName) is null) return NotFound();
        var usedBy = (await definitions.LoadTypeDefinitionsAsync()).Where(type => type.Parts.Any(part => part.PartDefinition.Name == partName)).Select(type => type.Name).ToArray();
        if (usedBy.Length > 0) return Problem($"The part is on {string.Join(", ", usedBy)}; detach it first.", statusCode: StatusCodes.Status409Conflict);

        await locks.EnsurePartChangeAsync(partName, DefinitionChanges.Deleted);
        await definitions.DeletePartDefinitionAsync(partName);
        return NoContent();
    }

    public async Task<IResult> PartSettingsAsync(string partName)
    {
        if (!await authorization.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes)) return Forbid();
        var part = await definitions.LoadPartDefinitionAsync(partName);
        if (part is null) return NotFound();
        var usedBy = (await definitions.LoadTypeDefinitionsAsync()).Where(type => type.Parts.Any(candidate => candidate.PartDefinition.Name == partName)).Select(type => type.Name).ToArray();
        return Ok(new ContentPartSettingsModel(
            part.Name,
            Clone(part.Settings),
            [.. part.Fields.Select(field => new PartFieldSettingsModel(field.Name, field.FieldDefinition.Name, Clone(field.Settings)))],
            usedBy));
    }

    public async Task<IResult> SetPartSettingsAsync(string partName, [FromBody] DefinitionSettingsRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadPartDefinitionAsync(partName) is null) return NotFound();
        if (request.Settings is null) return Invalid("Settings are required.");

        await locks.EnsurePartChangeAsync(partName, DefinitionChanges.SettingsChanged);
        await definitions.AlterPartDefinitionAsync(partName, part => part.ReplaceSettings(request.Settings));
        return NoContent();
    }

    public async Task<IResult> SetFieldSettingsAsync(string partName, string fieldName, [FromBody] DefinitionSettingsRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var field = (await definitions.LoadPartDefinitionAsync(partName))?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null) return NotFound();
        if (request.Settings is null) return Invalid("Settings are required.");

        await locks.EnsureFieldChangeAsync(partName, fieldName, DefinitionChanges.SettingsChanged);
        await definitions.AlterPartDefinitionAsync(partName, part => part.WithField(field.Name, builder => builder.ReplaceSettings(request.Settings)));
        return NoContent();
    }

    /// <summary>Renames a field for people: its display name. The technical name is the stored content's key and does not change.</summary>
    public async Task<IResult> RenameFieldAsync(string partName, string fieldName, [FromBody] FieldDisplayNameRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var field = (await definitions.LoadPartDefinitionAsync(partName))?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null) return NotFound();

        await definitions.AlterPartDefinitionAsync(partName, part => part.WithField(field.Name, builder => builder.WithDisplayName(request.DisplayName.Trim())));
        return NoContent();
    }

    public async Task<IResult> RemoveFieldAsync(string partName, string fieldName)
    {
        if (!await CanEditAsync()) return Forbid();
        var field = (await definitions.LoadPartDefinitionAsync(partName))?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null) return NotFound();

        await locks.EnsureFieldChangeAsync(partName, fieldName, DefinitionChanges.Removed);
        await definitions.AlterPartDefinitionAsync(partName, part => part.RemoveField(field.Name));
        return NoContent();
    }

    /// <summary>Orders a part's fields: each named field takes its index as its position.</summary>
    public async Task<IResult> OrderFieldsAsync(string partName, [FromBody] DefinitionOrderRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var part = await definitions.LoadPartDefinitionAsync(partName);
        if (part is null) return NotFound();
        var names = request.Names ?? [];
        if (names.Any(name => part.Fields.All(field => !string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)))) return Invalid("Every name must be a field of the part.");

        await definitions.AlterPartDefinitionAsync(partName, builder =>
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
