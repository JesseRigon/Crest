using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Crest.Admin;
using Crest.ContentFields.Settings;
using Crest.ContentFields.ViewModels;
using Crest.ContentManagement;
using Crest.ContentManagement.Metadata;
using Crest.ContentManagement.Metadata.Models;
using Crest.Contents;
using Crest.Modules;

namespace Crest.ContentFields.Controllers;

[RequireFeatures(Users.UserConstants.Features.Users)]
[Admin]
public sealed class UserPickerAdminController : Controller
{
    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly IEnumerable<IUserPickerResultProvider> _resultProviders;

    public UserPickerAdminController(
        IContentDefinitionManager contentDefinitionManager,
        IAuthorizationService authorizationService,
        IEnumerable<IUserPickerResultProvider> resultProviders
        )
    {
        _contentDefinitionManager = contentDefinitionManager;
        _authorizationService = authorizationService;
        _resultProviders = resultProviders;
    }

    [Admin("ContentFields/SearchUsers", "SearchUsers")]
    public async Task<IActionResult> SearchUsers(string part, string field, string contentType, string query)
    {
        if (string.IsNullOrWhiteSpace(part) || string.IsNullOrWhiteSpace(field) || string.IsNullOrWhiteSpace(contentType))
        {
            return BadRequest("Part, field and contentType are required parameters");
        }

        if (!await _authorizationService.AuthorizeContentTypeAsync(User, CommonPermissions.EditContent, contentType, User.FindFirstValue(ClaimTypes.NameIdentifier)))
        {
            return Forbid();
        }

        var partFieldDefinition = (await _contentDefinitionManager.GetPartDefinitionAsync(part))?.Fields
            .FirstOrDefault(f => f.Name == field);

        var fieldSettings = partFieldDefinition?.GetSettings<UserPickerFieldSettings>();
        if (fieldSettings == null)
        {
            return BadRequest("Unable to find field definition");
        }

        var editor = partFieldDefinition.Editor() ?? "Default";

        var resultProvider = _resultProviders.FirstOrDefault(p => p.Name == editor)
            ?? _resultProviders.FirstOrDefault(p => p.Name == "Default");

        if (resultProvider == null)
        {
            return new ObjectResult(new List<UserPickerResult>());
        }

        var results = await resultProvider.Search(new UserPickerSearchContext
        {
            Query = query,
            DisplayAllUsers = fieldSettings.DisplayAllUsers,
            Roles = fieldSettings.DisplayedRoles,
            PartFieldDefinition = partFieldDefinition,
        });

        return new ObjectResult(results.Select(r => new VueMultiselectUserViewModel() { Id = r.UserId, DisplayText = r.DisplayText, IsEnabled = r.IsEnabled }));
    }
}
