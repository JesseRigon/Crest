using Grove.ContentTypes;
using Grove.Fields;
using Grove.Models;
using Grove.Services;
using Grove.Settings;
using Grove.Api;
using Grove.Validation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Settings;
using Grove.Contents;

namespace Grove.OptionPicker.Api;

/// <summary>
/// <c>api/grove/option-picker/attachment</c>: reads and writes a field's picker binding.
/// </summary>
public sealed class OptionPickerAttachmentsApi : ApiContributor
{
    public override string Name => "OptionPickerAttachments";

    public override string FeatureId => OptionPickerConstants.FeatureId;

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("option-picker/attachment");
        routes.MapGet("", (HttpContext http, [FromQuery] string part, [FromQuery] string field) => ApiHandlers.For<OptionPickerAttachmentsHandlers>(http).GetAsync(part, field));
        routes.MapPut("", (HttpContext http, [FromBody] OptionPickerAttachmentRequest request) => ApiHandlers.For<OptionPickerAttachmentsHandlers>(http).PutAsync(request));
        routes.MapDelete("", (HttpContext http, [FromQuery] string part, [FromQuery] string field) => ApiHandlers.For<OptionPickerAttachmentsHandlers>(http).DeleteAsync(part, field));
    }
}

// The admin surface behind "connect this field to an option source": reads and
// writes a field's OptionPickerFieldSettings - the INSTANCE layer, where source,
// displayed columns, sort, search, filters/dependencies, required and the rest live.
// Editing is definition work (it rewrites the part definition), so it is gated by
// the stock content-definition permission, and there is deliberately no per-user
// override surface: what the admin configures here is the behaviour for everyone.
public sealed class OptionPickerAttachmentsHandlers(
    IContentDefinitionManager contentDefinitionManager,
    IEnumerable<IOptionSourceProvider> sourceProviders,
    IAuthorizationService authorizationService,
    DefinitionLockGuard locks) : ApiHandlers
{
    /// <summary>The field's current picker binding. 200 with IsOptionPicker=false
    /// for a field of another type (the PUT would convert it); 200 with
    /// FieldExists=false for a name not yet on the part.</summary>
    public async Task<IResult> GetAsync(string part, string field)
    {
        if (!await authorizationService.AuthorizeAsync(User, ContentTypesPermissions.ViewContentTypes))
        {
            return Forbid();
        }

        var partDefinition = await contentDefinitionManager.GetPartDefinitionAsync(part);
        var fieldDefinition = partDefinition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, field, StringComparison.OrdinalIgnoreCase));

        if (fieldDefinition is null)
        {
            return Ok(new OptionPickerAttachmentModel(false, null, false, new OptionPickerFieldSettings()));
        }

        var isPicker = string.Equals(fieldDefinition.FieldDefinition?.Name, nameof(OptionPickerField), StringComparison.Ordinal);
        return Ok(new OptionPickerAttachmentModel(
            true,
            fieldDefinition.FieldDefinition?.Name,
            isPicker,
            fieldDefinition.GetSettings<OptionPickerFieldSettings>() ?? new OptionPickerFieldSettings()));
    }

    /// <summary>
    /// Binds a field to an option source and writes its full instance settings.
    /// Creates the field when absent; converts it when it has another type (stored
    /// content is untouched - the field's data stays in each item's document).
    /// </summary>
    public async Task<IResult> PutAsync(OptionPickerAttachmentRequest request)
    {
        if (!await authorizationService.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes))
        {
            return Forbid();
        }

        var settings = request.Settings ?? new OptionPickerFieldSettings();
        var (providerKey, _) = OptionSourceKeys.Split(settings.SourceKey);
        if (!sourceProviders.Any(provider => string.Equals(provider.Key, providerKey, StringComparison.OrdinalIgnoreCase)))
        {
            return Problem($"There is no option source provider named '{providerKey}'.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Same conversion rule as the content-part-lists attach path: WithField keeps an
        // existing field's TYPE, so a field of another type is removed first -
        // otherwise the settings would say "option picker" while the field stays
        // what it was.
        var existing = await contentDefinitionManager.GetPartDefinitionAsync(request.Part);
        var current = existing?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, request.Field, StringComparison.OrdinalIgnoreCase));

        var position = current?.Settings?["ContentPartFieldSettings"]?["Position"]?.ToString();

        // A locked field keeps its type and binding; 409 via the lock filter.
        if (current is not null)
        {
            await locks.EnsureFieldChangeAsync(request.Part, request.Field, string.Equals(current.FieldDefinition?.Name, nameof(OptionPickerField), StringComparison.Ordinal) ? DefinitionChanges.SettingsChanged : DefinitionChanges.Retyped);
        }

        if (current is not null && !string.Equals(current.FieldDefinition?.Name, nameof(OptionPickerField), StringComparison.Ordinal))
        {
            await contentDefinitionManager.AlterPartDefinitionAsync(request.Part, part => part.RemoveField(request.Field));
        }

        await contentDefinitionManager.AlterPartDefinitionAsync(request.Part, part => part
            .WithField(request.Field, field =>
            {
                field
                    .OfType(nameof(OptionPickerField))
                    .WithDisplayName(request.DisplayName
                        ?? current?.Settings?["ContentPartFieldSettings"]?["DisplayName"]?.ToString()
                        ?? request.Field)
                    // Full replacement, property by property: MergeSettings merges
                    // JSON, and a merged array would keep rows the admin deleted.
                    .MergeSettings<OptionPickerFieldSettings>(target =>
                    {
                        target.SourceKey = settings.SourceKey;
                        target.Multiple = settings.Multiple;
                        target.Required = settings.Required;
                        target.Placeholder = settings.Placeholder ?? string.Empty;
                        target.Columns = settings.Columns ?? [];
                        target.DisplayTemplate = settings.DisplayTemplate;
                        target.TitlePattern = settings.TitlePattern;
                        target.SearchColumns = settings.SearchColumns ?? [];
                        target.SortColumns = settings.SortColumns ?? [];
                        target.Filters = settings.Filters ?? [];
                    });

                if (!string.IsNullOrWhiteSpace(position))
                {
                    field.WithPosition(position);
                }
            }));

        return NoContent();
    }

    /// <summary>Removes an option picker FIELD from the part definition. Only picker
    /// fields - removing other field types is general definition editing, out of this
    /// API's scope. Stored content is untouched (data stays in each item's
    /// document).</summary>
    public async Task<IResult> DeleteAsync(string part, string field)
    {
        if (!await authorizationService.AuthorizeAsync(User, ContentTypesPermissions.EditContentTypes))
        {
            return Forbid();
        }

        var partDefinition = await contentDefinitionManager.GetPartDefinitionAsync(part);
        var fieldDefinition = partDefinition?.Fields.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, field, StringComparison.OrdinalIgnoreCase));

        if (fieldDefinition is null)
        {
            return NotFound();
        }

        if (!string.Equals(fieldDefinition.FieldDefinition?.Name, nameof(OptionPickerField), StringComparison.Ordinal))
        {
            return Problem("Only option picker fields can be removed here.", statusCode: StatusCodes.Status400BadRequest);
        }

        await locks.EnsureFieldChangeAsync(part, field, DefinitionChanges.Removed);
        await contentDefinitionManager.AlterPartDefinitionAsync(part, definition => definition.RemoveField(field));
        return NoContent();
    }
}

/// <summary>A field's current picker binding, as the settings editor reads it.</summary>
public sealed record OptionPickerAttachmentModel(
    bool FieldExists,
    string? FieldType,
    bool IsOptionPicker,
    OptionPickerFieldSettings Settings);

/// <summary>The settings editor's save payload. Part is the part DEFINITION name -
/// for fields directly on a type, Grove's implicit part shares the type's name.</summary>
public sealed record OptionPickerAttachmentRequest(
    [property: NotEmpty] string Part,
    [property: NotEmpty] string Field,
    string? DisplayName,
    OptionPickerFieldSettings? Settings);
