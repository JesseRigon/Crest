using Grove.Access;
using Grove.Api;
using Grove.Contents;
using Grove.Contents.Management;
using Grove.Entities;
using Grove.Infrastructure.Settings;
using Grove.Parties.Constants;
using Grove.Parties.PartyTypes;
using Grove.Parties.Services;
using Grove.Parties.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Parties.Api;

/// <summary>
/// <c>api/grove/parties</c>: party roles and Individual organizations: "make this person a
/// customer" (an organization role given to a person lands on their Individual organization,
/// created when missing), converting an Individual organization into an ordinary one when the
/// tenant allows it, and that tenant setting.
/// </summary>
public sealed class PartyRolesApi : ApiContributor
{
    public override string Name => "PartyRoles";

    public override string FeatureId => "Grove.Parties";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("parties");
        routes.MapPost("{partyId}/roles", (HttpContext http, [FromRoute] string partyId, [FromBody] AddPartyRoleRequest request) => ApiHandlers.For<PartyRolesHandlers>(http).AddRoleAsync(partyId, request));
        routes.MapPost("{partyId}/convert-to-ordinary", (HttpContext http, [FromRoute] string partyId) => ApiHandlers.For<PartyRolesHandlers>(http).ConvertAsync(partyId));
        routes.MapGet("settings", ApiHandlers.Handle<PartyRolesHandlers>(h => h.GetSettingsAsync()));
        routes.MapPut("settings", (HttpContext http, [FromBody] PartiesSettingsModel request) => ApiHandlers.For<PartyRolesHandlers>(http).SetSettingsAsync(request));
    }
}

public sealed class PartyRolesHandlers(
    IPartyRoleService roles,
    IIndividualOrganizationService individuals,
    PartyTypeCatalog catalog,
    IContentManager contentManager,
    IAuthorizationService authorizationService,
    IAccessDecision decision,
    ICallerContextAccessor callers,
    ISiteService siteService) : ApiHandlers
{
    public async Task<IResult> AddRoleAsync(string partyId, AddPartyRoleRequest request)
    {
        var type = catalog.Find(request.TypeKey);
        if (type is null || type.Kind != PartyTypeKinds.Tenant || string.IsNullOrEmpty(type.ContentType)
            || type.Key is PartiesConstants.PartyTypeKeys.Contacts or PartiesConstants.PartyTypeKeys.Organizations)
        {
            return BadRequest(PartyRequests.Errors($"'{request.TypeKey}' is not a registered role party type."));
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
            return BadRequest(PartyRequests.Errors(exception.Message));
        }
    }

    public async Task<IResult> ConvertAsync(string partyId)
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
            return BadRequest(PartyRequests.Errors(exception.Message));
        }
    }

    public async Task<IResult> GetSettingsAsync()
    {
        if (!await decision.AllowsAsync(callers.Current, "ManageSettings", cancellationToken: HttpContext.RequestAborted))
        {
            return Forbid();
        }

        var settings = await siteService.GetSettingsAsync<PartiesSettings>();
        return Ok(new PartiesSettingsModel(settings.AllowIndividualConversion));
    }

    public async Task<IResult> SetSettingsAsync(PartiesSettingsModel request)
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
