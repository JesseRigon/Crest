using Crest.Entities;
using Crest.Access;
using Crest.ContentManagement;
using Crest.Contents;
using Crest.Parties.Constants;
using Crest.Parties.PartyTypes;
using Crest.Parties.Services;
using Crest.Parties.ViewModels;
using Crest.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Parties.Controllers;


/// <summary>
/// Party roles and Individual organizations (ruling 2026-10-14): "make this person a
/// customer" (an organization role given to a person lands on their Individual
/// organization, created when missing), converting an Individual organization into an
/// ordinary one when the tenant allows it, and that tenant setting.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/parties")]
public sealed class PartyRolesController(
    IPartyRoleService roles,
    IIndividualOrganizationService individuals,
    PartyTypeCatalog catalog,
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    IAccessDecision decision,
    ICallerContextAccessor callers,
    ISiteService siteService) : ControllerBase
{
    [HttpPost("{partyId}/roles")]
    public async Task<ActionResult<PartyRoleResult>> AddRoleAsync(string partyId, AddPartyRoleRequest request)
    {
        var type = catalog.Find(request.TypeKey);
        if (type is null || type.Kind != PartyTypeKinds.Tenant || string.IsNullOrEmpty(type.ContentType)
            || type.Key is PartiesConstants.PartyTypeKeys.Contacts or PartiesConstants.PartyTypeKeys.Organizations)
        {
            return BadRequest(new { errors = new[] { $"'{request.TypeKey}' is not a registered role party type." } });
        }

        try
        {
            return Ok(await roles.AddRoleAsync(type.ContentType, partyId, callers.Current, HttpContext.RequestAborted));
        }
        catch (AccessDeniedException)
        {
            return Forbid();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { errors = new[] { exception.Message } });
        }
    }

    [HttpPost("{partyId}/convert-to-ordinary")]
    public async Task<IActionResult> ConvertAsync(string partyId)
    {
        var (organization, failure) = await PartyRequests.LoadAsync(contentManager, authorizationService, User, partyId, CommonPermissions.EditContent, PartiesConstants.ContentTypes.Organization);
        if (failure is not null) return failure;

        try
        {
            await individuals.ConvertToOrdinaryAsync(organization!, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { errors = new[] { exception.Message } });
        }
    }

    [HttpGet("settings")]
    public async Task<ActionResult<PartiesSettingsModel>> GetSettingsAsync()
    {
        if (!await decision.AllowsAsync(callers.Current, "ManageSettings", cancellationToken: HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var settings = await siteService.GetSettingsAsync<PartiesSettings>();
        return Ok(new PartiesSettingsModel(settings.AllowIndividualConversion));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<PartiesSettingsModel>> SetSettingsAsync(PartiesSettingsModel request)
    {
        if (!await decision.AllowsAsync(callers.Current, "ManageSettings", cancellationToken: HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var site = await siteService.LoadSiteSettingsAsync();
        site.Put(new PartiesSettings { AllowIndividualConversion = request.AllowIndividualConversion });
        await siteService.UpdateSiteSettingsAsync(site);
        return Ok(request);
    }
}
