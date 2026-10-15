#nullable enable

using Grove.Api;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Grove.Contents.Fields.Settings;
using Grove.Contents.Management;
using Grove.Contents.Management.Metadata;
using Grove.Contents.Management.Metadata.Models;
using Grove.Contents;

namespace Grove.Contents.Fields.Api;

/// <summary>
/// <c>api/grove/content-fields/users</c>: the user picker's search (docs/blazor-display.md rework step 7): the users the field's result
/// provider finds for a query, within the field's role filter, for a caller who may edit items of
/// the type. Used by the Blazor <c>UserPickerFieldEditor</c> and the classic editor; replaces the
/// MVC <c>UserPickerAdminController.SearchUsers</c>. Registered with the user picker field, when
/// the users feature is enabled.
/// </summary>
public sealed class UserPickerApi : ApiContributor
{
    public override string Name => "UserPicker";

    public override string FeatureId => "Grove.Contents.Fields";

    public override void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("content-fields/users", (
            HttpContext http,
            [FromQuery] string? part,
            [FromQuery] string? field,
            [FromQuery] string? contentType,
            [FromQuery] string? query) => ApiHandlers.For<UserPickerHandlers>(http).SearchAsync(part, field, contentType, query));
    }
}

public sealed class UserPickerHandlers(
    IContentDefinitionManager contentDefinitionManager,
    IAuthorizationService authorizationService,
    IEnumerable<IUserPickerResultProvider> resultProviders) : ApiHandlers
{
    public async Task<IResult> SearchAsync(string? part, string? field, string? contentType, string? query)
    {
        if (string.IsNullOrWhiteSpace(part) || string.IsNullOrWhiteSpace(contentType))
        {
            return BadRequest("Part and contentType are required parameters");
        }

        // The common part's owner editor: any user, for a caller who may change owners.
        if (string.IsNullOrWhiteSpace(field) && part == "CommonPart")
        {
            if (!await authorizationService.AuthorizeContentTypeAsync(User, CommonPermissions.EditContentOwner, contentType!, User.FindFirstValue(ClaimTypes.NameIdentifier)))
            {
                return Forbid();
            }

            var ownerProvider = resultProviders.FirstOrDefault(candidate => candidate.Name == "Default");
            if (ownerProvider is null)
            {
                return Ok(Array.Empty<UserPickerSearchResult>());
            }

            var owners = await ownerProvider.Search(new UserPickerSearchContext { Query = query ?? string.Empty, DisplayAllUsers = true, Roles = [] });
            return Ok(owners.Select(result => new UserPickerSearchResult(result.UserId, result.DisplayText, result.IsEnabled)).ToArray());
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            return BadRequest("Part, field and contentType are required parameters");
        }

        if (!await authorizationService.AuthorizeContentTypeAsync(User, CommonPermissions.EditContent, contentType!, User.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            return Forbid();
        }

        var partFieldDefinition = (await contentDefinitionManager.GetPartDefinitionAsync(part))?.Fields
            .FirstOrDefault(definition => definition.Name == field);

        var settings = partFieldDefinition?.GetSettings<UserPickerFieldSettings>();
        if (settings is null || partFieldDefinition is null)
        {
            return BadRequest("Unable to find field definition");
        }

        var editor = partFieldDefinition.Editor() ?? "Default";
        var provider = resultProviders.FirstOrDefault(candidate => candidate.Name == editor)
            ?? resultProviders.FirstOrDefault(candidate => candidate.Name == "Default");
        if (provider is null)
        {
            return Ok(Array.Empty<UserPickerSearchResult>());
        }

        var results = await provider.Search(new UserPickerSearchContext
        {
            Query = query ?? string.Empty,
            DisplayAllUsers = settings.DisplayAllUsers,
            Roles = settings.DisplayedRoles,
            PartFieldDefinition = partFieldDefinition,
        });

        return Ok(results.Select(result => new UserPickerSearchResult(result.UserId, result.DisplayText, result.IsEnabled)).ToArray());
    }
}

/// <summary>One user a search found.</summary>
public sealed record UserPickerSearchResult(string Id, string DisplayText, bool IsEnabled);
