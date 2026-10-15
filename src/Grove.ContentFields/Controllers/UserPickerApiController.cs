using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Grove.ContentFields.Settings;
using Grove.ContentManagement;
using Grove.ContentManagement.Metadata;
using Grove.ContentManagement.Metadata.Models;
using Grove.Contents;
using Grove.Modules;

namespace Grove.ContentFields.Controllers;

/// <summary>
/// The user picker's search (docs/blazor-display.md rework step 7): the users the field's result
/// provider finds for a query, within the field's role filter, for a caller who may edit items of
/// the type. Used by the Blazor <c>UserPickerFieldEditor</c> and the classic editor; replaces the
/// MVC <c>UserPickerAdminController.SearchUsers</c>.
/// </summary>
[ApiController]
[RequireFeatures(Users.UserConstants.Features.Users)]
[Route("api/grove/content-fields/users")]
public sealed class UserPickerApiController(
    IContentDefinitionManager contentDefinitionManager,
    IAuthorizationService authorizationService,
    IEnumerable<IUserPickerResultProvider> resultProviders) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserPickerSearchResult[]>> SearchAsync(string part, string field, string contentType, string query)
    {
        if (string.IsNullOrWhiteSpace(part) || string.IsNullOrWhiteSpace(contentType))
        {
            return BadRequest("Part and contentType are required parameters");
        }

        // The common part's owner editor: any user, for a caller who may change owners.
        if (string.IsNullOrWhiteSpace(field) && part == "CommonPart")
        {
            if (!await authorizationService.AuthorizeContentTypeAsync(User, CommonPermissions.EditContentOwner, contentType, User.FindFirstValue(ClaimTypes.NameIdentifier)))
            {
                return Forbid();
            }

            var ownerProvider = resultProviders.FirstOrDefault(candidate => candidate.Name == "Default");
            if (ownerProvider is null)
            {
                return Ok(Array.Empty<UserPickerSearchResult>());
            }

            var owners = await ownerProvider.Search(new UserPickerSearchContext { Query = query, DisplayAllUsers = true, Roles = [] });
            return Ok(owners.Select(result => new UserPickerSearchResult(result.UserId, result.DisplayText, result.IsEnabled)).ToArray());
        }

        if (string.IsNullOrWhiteSpace(field))
        {
            return BadRequest("Part, field and contentType are required parameters");
        }

        if (!await authorizationService.AuthorizeContentTypeAsync(User, CommonPermissions.EditContent, contentType, User.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            return Forbid();
        }

        var partFieldDefinition = (await contentDefinitionManager.GetPartDefinitionAsync(part))?.Fields
            .FirstOrDefault(definition => definition.Name == field);

        var settings = partFieldDefinition?.GetSettings<UserPickerFieldSettings>();
        if (settings is null)
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
            Query = query,
            DisplayAllUsers = settings.DisplayAllUsers,
            Roles = settings.DisplayedRoles,
            PartFieldDefinition = partFieldDefinition,
        });

        return Ok(results.Select(result => new UserPickerSearchResult(result.UserId, result.DisplayText, result.IsEnabled)).ToArray());
    }
}

/// <summary>One user a search found.</summary>
public sealed record UserPickerSearchResult(string Id, string DisplayText, bool IsEnabled);
