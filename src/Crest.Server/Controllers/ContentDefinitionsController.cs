using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.ContentManagement.Metadata.Settings;
using Crest.Contents;
using Crest.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Controllers;

public sealed record CreateContentTypeRequest(string Name, string? DisplayName, string? Stereotype = null);

public sealed record CreateContentPartRequest(string Name, string? DisplayName = null, string? Description = null);

/// <summary>A definition's settings as stored: every section, keyed by settings type name.</summary>
public sealed record DefinitionSettingsRequest(JsonObject? Settings, string? DisplayName = null);

public sealed record DefinitionOrderRequest(IReadOnlyList<string> Names);

public sealed record FieldDisplayNameRequest(string DisplayName);

public sealed record TypePartSettingsModel(string Name, string PartName, JsonObject Settings);

public sealed record ContentTypeSettingsModel(string Name, string DisplayName, JsonObject Settings, IReadOnlyList<TypePartSettingsModel> Parts);

public sealed record PartFieldSettingsModel(string Name, string FieldType, JsonObject Settings);

public sealed record ContentPartSettingsModel(string Name, JsonObject Settings, IReadOnlyList<PartFieldSettingsModel> Fields, IReadOnlyList<string> UsedByTypes);

/// <summary>
/// The content definition editor's writes (Content Types and Content Parts pages, step 7 of
/// docs/blazor-display.md, replacing the MVC ContentTypes admin): create and delete types and parts,
/// remove, rename and reorder fields and parts, and every settings section of a type, a part on a
/// type, a part and a field - settings edited as their stored JSON, so a section a module adds
/// needs no editor. Definition locks hold on every path (docs/content-items.md › Definition locks).
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/content-types")]
public sealed class ContentDefinitionsController(
    IContentDefinitionManager definitions,
    IAuthorizationService authorization,
    DefinitionLockGuard locks) : ControllerBase
{
    private static readonly Regex s_technicalName = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

    [HttpPost]
    public async Task<IActionResult> CreateTypeAsync([FromBody] CreateContentTypeRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (!IsTechnicalName(request.Name)) return Invalid("A type name starts with a letter and holds letters, digits and underscores only.");
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

        return Created($"api/crest/content-types/{request.Name}", null);
    }

    [HttpDelete("{contentType}")]
    public async Task<IActionResult> DeleteTypeAsync(string contentType)
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

    [HttpGet("{contentType}/settings")]
    public async Task<ActionResult<ContentTypeSettingsModel>> TypeSettingsAsync(string contentType)
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

    [HttpPut("{contentType}/settings")]
    public async Task<IActionResult> SetTypeSettingsAsync(string contentType, [FromBody] DefinitionSettingsRequest request)
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

    [HttpPut("{contentType}/parts/{partName}/settings")]
    public async Task<IActionResult> SetTypePartSettingsAsync(string contentType, string partName, [FromBody] DefinitionSettingsRequest request)
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
    [HttpPut("{contentType}/parts/order")]
    public async Task<IActionResult> OrderTypePartsAsync(string contentType, [FromBody] DefinitionOrderRequest request)
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

    [HttpPost("parts")]
    public async Task<IActionResult> CreatePartAsync([FromBody] CreateContentPartRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (!IsTechnicalName(request.Name)) return Invalid("A part name starts with a letter and holds letters, digits and underscores only.");
        if (await definitions.LoadPartDefinitionAsync(request.Name) is not null) return Problem($"A content part named '{request.Name}' exists.", statusCode: StatusCodes.Status409Conflict);

        await definitions.AlterPartDefinitionAsync(request.Name, part =>
        {
            part.Attachable();
            if (!string.IsNullOrWhiteSpace(request.DisplayName)) part.WithDisplayName(request.DisplayName.Trim());
            if (!string.IsNullOrWhiteSpace(request.Description)) part.WithDescription(request.Description.Trim());
        });
        return Created($"api/crest/content-types/parts/{request.Name}", null);
    }

    [HttpDelete("parts/{partName}")]
    public async Task<IActionResult> DeletePartAsync(string partName)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadPartDefinitionAsync(partName) is null) return NotFound();
        var usedBy = (await definitions.LoadTypeDefinitionsAsync()).Where(type => type.Parts.Any(part => part.PartDefinition.Name == partName)).Select(type => type.Name).ToArray();
        if (usedBy.Length > 0) return Problem($"The part is on {string.Join(", ", usedBy)}; detach it first.", statusCode: StatusCodes.Status409Conflict);

        await locks.EnsurePartChangeAsync(partName, DefinitionChanges.Deleted);
        await definitions.DeletePartDefinitionAsync(partName);
        return NoContent();
    }

    [HttpGet("parts/{partName}/settings")]
    public async Task<ActionResult<ContentPartSettingsModel>> PartSettingsAsync(string partName)
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

    [HttpPut("parts/{partName}/settings")]
    public async Task<IActionResult> SetPartSettingsAsync(string partName, [FromBody] DefinitionSettingsRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (await definitions.LoadPartDefinitionAsync(partName) is null) return NotFound();
        if (request.Settings is null) return Invalid("Settings are required.");

        await locks.EnsurePartChangeAsync(partName, DefinitionChanges.SettingsChanged);
        await definitions.AlterPartDefinitionAsync(partName, part => part.ReplaceSettings(request.Settings));
        return NoContent();
    }

    [HttpPut("parts/{partName}/fields/{fieldName}/settings")]
    public async Task<IActionResult> SetFieldSettingsAsync(string partName, string fieldName, [FromBody] DefinitionSettingsRequest request)
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
    [HttpPut("parts/{partName}/fields/{fieldName}/display-name")]
    public async Task<IActionResult> RenameFieldAsync(string partName, string fieldName, [FromBody] FieldDisplayNameRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        var field = (await definitions.LoadPartDefinitionAsync(partName))?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.DisplayName)) return Invalid("A display name is required.");

        await definitions.AlterPartDefinitionAsync(partName, part => part.WithField(field.Name, builder => builder.WithDisplayName(request.DisplayName.Trim())));
        return NoContent();
    }

    [HttpDelete("parts/{partName}/fields/{fieldName}")]
    public async Task<IActionResult> RemoveFieldAsync(string partName, string fieldName)
    {
        if (!await CanEditAsync()) return Forbid();
        var field = (await definitions.LoadPartDefinitionAsync(partName))?.Fields.FirstOrDefault(candidate => string.Equals(candidate.Name, fieldName, StringComparison.OrdinalIgnoreCase));
        if (field is null) return NotFound();

        await locks.EnsureFieldChangeAsync(partName, fieldName, DefinitionChanges.Removed);
        await definitions.AlterPartDefinitionAsync(partName, part => part.RemoveField(field.Name));
        return NoContent();
    }

    /// <summary>Orders a part's fields: each named field takes its index as its position.</summary>
    [HttpPut("parts/{partName}/fields/order")]
    public async Task<IActionResult> OrderFieldsAsync(string partName, [FromBody] DefinitionOrderRequest request)
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

    private ObjectResult Invalid(string message) => Problem(message, statusCode: StatusCodes.Status400BadRequest);

    private static bool IsTechnicalName(string? name) => !string.IsNullOrWhiteSpace(name) && s_technicalName.IsMatch(name);

    private static JsonObject Clone(JsonObject? settings) => settings?.DeepClone() as JsonObject ?? [];
}
