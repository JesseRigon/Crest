using Grove.Infrastructure.Settings;
using YesSql;
using Grove.Entities;
using System.Security.Claims;
using Grove.Access;
using Grove.Access.Members.Constants;
using Grove.Access.Members.Models;
using Grove.Access.Members.Services;
using Microsoft.AspNetCore.Authorization;
using Grove.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Grove.Access.Members.Api;

public sealed record CreateMemberApiRequest(string UserName, string? Email, string? Password, string? PersonId, string OrganizationId, IReadOnlyList<string>? Roles);
public sealed record AddBindingRequest(string OrganizationId, IReadOnlyList<string>? Roles);
public sealed record SetActiveOrgRequest(string OrganizationId);
public sealed record ImpersonateRequest(string UserId);
public sealed record HierarchyEdgeRequest(string ReportId, string ManagerId);
public sealed record InviteToPortalRequest(string PersonId, string OrganizationId, string UserName, string? Email, IReadOnlyList<string>? Roles);
public sealed record OwnershipModel(string OrganizationId, string? OwnerUserId, bool CanTransfer);
public sealed record TransferOwnershipRequest(string UserId);

/// <summary>
/// Members over HTTP (docs/members.md): the member session (who, which organization), the
/// organization's members and their bindings, class conversion, the tenant's Members settings,
/// the reports-to hierarchy, the organization owner and impersonation.
/// </summary>
public sealed class MembersApi : ApiContributor
{
    public override string Name => "Members";

    public override string FeatureId => "Grove.Access.Members";

    public override void Map(IEndpointRouteBuilder group)
    {
        var routes = group.MapGroup("members");
        routes.MapGet("me", ApiHandlers.Handle<MembersHandlers>(h => h.MeAsync())).RequireAuthorization();
        routes.MapPost("active-org", (HttpContext http, [FromBody] SetActiveOrgRequest request) => ApiHandlers.For<MembersHandlers>(http).SetActiveOrgAsync(request)).RequireAuthorization();
        routes.MapGet("", (HttpContext http, [FromQuery] string organizationId) => ApiHandlers.For<MembersHandlers>(http).ListAsync(organizationId));
        routes.MapPost("", (HttpContext http, [FromBody] CreateMemberApiRequest request) => ApiHandlers.For<MembersHandlers>(http).CreateAsync(request));
        routes.MapPost("invite", (HttpContext http, [FromBody] InviteToPortalRequest request) => ApiHandlers.For<MembersHandlers>(http).InviteAsync(request));
        routes.MapPost("{userId}/bindings", (HttpContext http, [FromRoute] string userId, [FromBody] AddBindingRequest request) => ApiHandlers.For<MembersHandlers>(http).AddBindingAsync(userId, request));
        routes.MapDelete("{userId}/bindings/{organizationId}", (HttpContext http, [FromRoute] string userId, [FromRoute] string organizationId) => ApiHandlers.For<MembersHandlers>(http).RemoveBindingAsync(userId, organizationId));
        routes.MapGet("{userId}/classes", (HttpContext http, [FromRoute] string userId) => ApiHandlers.For<MembersHandlers>(http).GetClassesAsync(userId));
        routes.MapPost("{userId}/classes", (HttpContext http, [FromRoute] string userId, [FromBody] AddUserClassRequest request) => ApiHandlers.For<MembersHandlers>(http).AddClassAsync(userId, request));
        routes.MapDelete("{userId}/classes/{userClass}", (HttpContext http, [FromRoute] string userId, [FromRoute] string userClass) => ApiHandlers.For<MembersHandlers>(http).RemoveClassAsync(userId, userClass));
        routes.MapGet("settings", ApiHandlers.Handle<MembersHandlers>(h => h.GetSettingsAsync()));
        routes.MapPut("settings", (HttpContext http, [FromBody] MembersSettingsModel request) => ApiHandlers.For<MembersHandlers>(http).SetSettingsAsync(request));
        routes.MapPost("rebuild", ApiHandlers.Handle<MembersHandlers>(h => h.RebuildAsync()));
        routes.MapGet("hierarchy/{organizationId}", (HttpContext http, [FromRoute] string organizationId) => ApiHandlers.For<MembersHandlers>(http).GetHierarchyAsync(organizationId));
        routes.MapPost("hierarchy/{organizationId}/edges", (HttpContext http, [FromRoute] string organizationId, [FromBody] HierarchyEdgeRequest request) => ApiHandlers.For<MembersHandlers>(http).AddHierarchyEdgeAsync(organizationId, request));
        routes.MapDelete("hierarchy/{organizationId}/edges/{reportId}/{managerId}", (HttpContext http, [FromRoute] string organizationId, [FromRoute] string reportId, [FromRoute] string managerId) => ApiHandlers.For<MembersHandlers>(http).RemoveHierarchyEdgeAsync(organizationId, reportId, managerId));
        routes.MapGet("organizations/{organizationId}/owner", (HttpContext http, [FromRoute] string organizationId) => ApiHandlers.For<MembersHandlers>(http).GetOwnerAsync(organizationId));
        routes.MapPost("organizations/{organizationId}/owner", (HttpContext http, [FromRoute] string organizationId, [FromBody] TransferOwnershipRequest request) => ApiHandlers.For<MembersHandlers>(http).TransferOwnershipAsync(organizationId, request));
        routes.MapPost("impersonate", (HttpContext http, [FromBody] ImpersonateRequest request) => ApiHandlers.For<MembersHandlers>(http).ImpersonateAsync(request));
        routes.MapPost("impersonate/stop", ApiHandlers.Handle<MembersHandlers>(h => h.StopImpersonationAsync())).RequireAuthorization();
    }
}

public sealed class MembersHandlers(
    IMemberService memberService,
    Grove.Parties.Services.IPartyUserLinkService partyUserLink,
    YesSql.ISession session,
    MemberBindingService bindings,
    UserClassService userClasses,
    IUserHierarchyService hierarchy,
    Microsoft.AspNetCore.Identity.UserManager<Grove.Access.Users.IUser> userManager,
    Grove.Infrastructure.Settings.ISiteService siteService,
    MemberSessionService sessionService,
    MemberOrganizationDirectory organizationDirectory,
    MemberImpersonationService impersonationService,
    IAuthorizationService authorizationService,
    Grove.Access.IAccessDecision decision,
    Grove.Access.ICallerContextAccessor callerAccessor,
    Grove.Access.IOrganizationOwnership ownership) : ApiHandlers
{
    public async Task<IResult> MeAsync()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var member = userId is null ? null : await memberService.GetAsync(userId);
        // The caller is the authority on the organization and the impersonator; the session
        // only remembers the member's last choice for requests that do not name one.
        var caller = callerAccessor.Current;
        var activeOrg = caller?.OrganizationId ?? await sessionService.GetActiveOrganizationAsync(HttpContext);
        var impersonator = caller?.ImpersonatorUserId;

        var impersonatorName = impersonator is null
            ? null
            : MemberSessionService.Gated(HttpContext).Properties?.Items.TryGetValue(MemberSessionKeys.ImpersonatorUserName, out var name) == true ? name : impersonator;

        return Ok(new MemberSessionModel(
            member,
            activeOrg,
            impersonator,
            await organizationDirectory.GetAsync(member),
            impersonatorName,
            (await siteService.GetSiteSettingsAsync()).SiteName));
    }

    // The org switcher (ruling: one account, N bindings, per-org roles; the UI and
    // permissions follow the ACTIVE org). Members switch their own session; an
    // impersonating staff session switches the member's orgs the same way.
    public async Task<IResult> SetActiveOrgAsync(SetActiveOrgRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var member = userId is null ? null : await memberService.GetAsync(userId);
        if (member is null || !member.Bindings.Any(binding => binding.OrganizationId == request.OrganizationId))
        {
            return Forbid();
        }

        await sessionService.SetActiveOrganizationAsync(HttpContext, request.OrganizationId);
        return Ok();
    }

    public async Task<IResult> ListAsync(string organizationId)
    {
        if (!await CanManageAsync(new MemberOrganizationResource(organizationId)))
        {
            return Forbid();
        }

        return Ok(await memberService.ListByOrganizationAsync(organizationId, HttpContext.RequestAborted));
    }

    public async Task<IResult> CreateAsync(CreateMemberApiRequest request)
    {
        if (!await CanManageAsync(new MemberOrganizationResource(request.OrganizationId)))
        {
            return Forbid();
        }

        var errors = new Grove.Validation.ValidationErrors();
        var created = await memberService.CreateMemberAsync(
            new CreateMemberRequest(request.UserName, request.Email, request.Password, request.PersonId, request.OrganizationId, request.Roles),
            (key, message) => errors.Add(key, message),
            HttpContext.RequestAborted);

        if (created is null)
        {
            return ApiProblems.Validation(errors);
        }

        var member = await memberService.GetAsync(((Grove.Access.Users.Models.User)created).UserId, HttpContext.RequestAborted);
        return Ok(member);
    }

    /// <summary>
    /// Portal access as a promotion (ruling 2026-10-14): a contact Person who holds a position
    /// in the organization becomes a user linked to that person and bound to the organization;
    /// the first member is the organization's owner. The account has no password: the person
    /// sets one through the portal's password recovery.
    /// </summary>
    public async Task<IResult> InviteAsync(InviteToPortalRequest request)
    {
        if (!await CanManageAsync(new MemberOrganizationResource(request.OrganizationId)))
        {
            return Forbid();
        }

        if (await partyUserLink.GetPortalUserIdAsync(request.PersonId, HttpContext.RequestAborted) is not null)
        {
            return BadRequest(new { error = "The person already has an account." });
        }

        var holdsPosition = await session.QueryIndex<Grove.Parties.Indexes.PartyPositionIndex>(index =>
                index.ContentItemId == request.PersonId && index.OrganizationId == request.OrganizationId && index.Latest)
            .CountAsync(HttpContext.RequestAborted) > 0;
        if (!holdsPosition)
        {
            return BadRequest(new { error = "Only a person who holds a position in the organization can be invited to its portal." });
        }

        try
        {
            var errors = new Grove.Validation.ValidationErrors();
            var created = await memberService.CreateMemberAsync(
                new CreateMemberRequest(request.UserName, request.Email, null, request.PersonId, request.OrganizationId, request.Roles),
                (key, message) => errors.Add(key, message),
                HttpContext.RequestAborted);
            if (created is null)
            {
                return ApiProblems.Validation(errors);
            }

            return Ok(await memberService.GetAsync(((Grove.Access.Users.Models.User)created).UserId, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    public async Task<IResult> AddBindingAsync(string userId, AddBindingRequest request)
    {
        if (!await CanManageAsync(new MemberBindingResource(userId, request.OrganizationId)))
        {
            return Forbid();
        }

        try
        {
            var user = await RequireUserAsync(userId);
            // Giving a user their first member binding adds a class: that is conversion,
            // its own permission-gated action, never a side effect of the binding API.
            if (!UserClassSet.Has(user, UserClasses.Member))
            {
                return BadRequest(new { error = "The user is not a member; adding the member class is a class conversion." });
            }

            var binding = await bindings.AddAsync(user, request.OrganizationId, UserClasses.Member, request.Roles, HttpContext.RequestAborted);
            return Ok(new MemberOrgBindingModel(binding.OrganizationId, binding.Roles, binding.IsMemberAdmin));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    public async Task<IResult> RemoveBindingAsync(string userId, string organizationId)
    {
        if (!await CanManageAsync(new MemberBindingResource(userId, organizationId)))
        {
            return Forbid();
        }

        try
        {
            var user = await RequireUserAsync(userId);
            if (UserClassSet.Bindings(user, UserClasses.Member).Count() == 1)
            {
                return BadRequest(new { error = "This is the user's last member binding; removing the member class is a class conversion." });
            }

            await bindings.RemoveAsync(user, organizationId, UserClasses.Member, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    // Class conversion: adding or removing a class is the one action, gated by the
    // security-critical ConvertUserClass permission (staff only: it is in the ceiling).
    public async Task<IResult> GetClassesAsync(string userId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ConvertUserClass))
        {
            return Forbid();
        }

        return await userClasses.GetAsync(userId, HttpContext.RequestAborted) is { } model ? Ok(model) : NotFound();
    }

    public async Task<IResult> AddClassAsync(string userId, AddUserClassRequest request)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ConvertUserClass))
        {
            return Forbid();
        }

        try
        {
            return Ok(await userClasses.AddAsync(userId, request, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    public async Task<IResult> RemoveClassAsync(string userId, string userClass)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ConvertUserClass))
        {
            return Forbid();
        }

        try
        {
            return Ok(await userClasses.RemoveAsync(userId, userClass, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    // The Members tenant settings: whether members may be bound to the tenant's own
    // organization (default: blocked). Staff only (ManageMembers is in the ceiling).
    public async Task<IResult> GetSettingsAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ManageMembers))
        {
            return Forbid();
        }

        var settings = await siteService.GetSettingsAsync<MembersSettings>();
        return Ok(new MembersSettingsModel(settings.AllowMembersOnTenantOrganization));
    }

    public async Task<IResult> SetSettingsAsync(MembersSettingsModel request)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ManageMembers))
        {
            return Forbid();
        }

        var site = await siteService.LoadSiteSettingsAsync();
        site.Put(new MembersSettings { AllowMembersOnTenantOrganization = request.AllowMembersOnTenantOrganization });
        await siteService.UpdateSiteSettingsAsync(site);
        return Ok(request);
    }

    /// <summary>Rebuilds the connection index and the hierarchy from the binding records:
    /// drift repair, and the migration of existing data.</summary>
    public async Task<IResult> RebuildAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ConvertUserClass))
        {
            return Forbid();
        }

        return Ok(new { users = await bindings.RebuildAsync(HttpContext.RequestAborted) });
    }

    // The reports-to hierarchy of one organization (the staff hierarchy is the tenant
    // organization's). ManageOrgMembers against the organization: staff tenant-wide, a member
    // admin only in their own organization.
    public async Task<IResult> GetHierarchyAsync(string organizationId)
    {
        if (!await CanManageAsync(new MemberOrganizationResource(organizationId)))
        {
            return Forbid();
        }

        return Ok(await hierarchy.GetNodesAsync(organizationId, HttpContext.RequestAborted));
    }

    public async Task<IResult> AddHierarchyEdgeAsync(string organizationId, HierarchyEdgeRequest request)
    {
        if (!await CanManageAsync(new MemberOrganizationResource(organizationId)))
        {
            return Forbid();
        }

        try
        {
            await hierarchy.AddManagerAsync(organizationId, request.ReportId, request.ManagerId, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    public async Task<IResult> RemoveHierarchyEdgeAsync(string organizationId, string reportId, string managerId)
    {
        if (!await CanManageAsync(new MemberOrganizationResource(organizationId)))
        {
            return Forbid();
        }

        try
        {
            await hierarchy.RemoveManagerAsync(organizationId, reportId, managerId, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    // The organization owner (docs/permissions.md › The organization owner). Reading takes
    // ManageOrgMembers against the organization, or being its owner; a transfer is started by
    // staff holding TransferOrganizationOwnership or by the current owner, no one else.
    public async Task<IResult> GetOwnerAsync(string organizationId)
    {
        var owner = await ownership.GetOwnerAsync(organizationId, HttpContext.RequestAborted);
        var isOwner = owner is not null && owner == callerAccessor.Current?.UserId;
        if (!isOwner && !await CanManageAsync(new MemberOrganizationResource(organizationId)))
        {
            return Forbid();
        }

        return Ok(new OwnershipModel(organizationId, owner, isOwner || await CanTransferAsTenantAsync()));
    }

    public async Task<IResult> TransferOwnershipAsync(string organizationId, TransferOwnershipRequest request)
    {
        var caller = callerAccessor.Current;
        var owner = await ownership.GetOwnerAsync(organizationId, HttpContext.RequestAborted);
        var isOwner = owner is not null && caller?.UserId == owner;
        if (!isOwner && !await CanTransferAsTenantAsync())
        {
            return Forbid();
        }

        try
        {
            await ownership.TransferAsync(organizationId, request.UserId, HttpContext.RequestAborted);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }

        return Ok(new OwnershipModel(organizationId, request.UserId, await CanTransferAsTenantAsync()));
    }

    /// <summary>The tenant permission, held at tenant reach only (staff; it is on the member ceiling).</summary>
    private async Task<bool> CanTransferAsTenantAsync() =>
        callerAccessor.Current is { OrganizationId: null } caller
        && await decision.AllowsAsync(caller, Permissions.MembersPermissions.TransferOrganizationOwnership.Name, null, HttpContext.RequestAborted);

    // Through the user store (the request's unit of work), not a session query of its own.
    private async Task<Grove.Access.Users.Models.User> RequireUserAsync(string userId) =>
        await userManager.FindByIdAsync(userId) as Grove.Access.Users.Models.User
            ?? throw new InvalidOperationException("Unknown user.");

    public async Task<IResult> ImpersonateAsync(ImpersonateRequest request)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ImpersonateMembers))
        {
            return Forbid();
        }

        try
        {
            await impersonationService.StartAsync(request.UserId);
            return Ok();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    // Deliberately NOT permission-gated: the session's recorded impersonator is the
    // authority - anyone inside an impersonation may always leave it (the underlying
    // staff identity already proved ImpersonateMembers to get in).
    public async Task<IResult> StopImpersonationAsync()
    {
        try
        {
            await impersonationService.StopAsync();
            // Back to the admin, where the restored staff session acts.
            var adminPath = HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Options.IOptions<Grove.Middlewares.BlazorAdminThemeOptions>))
                is Microsoft.Extensions.Options.IOptions<Grove.Middlewares.BlazorAdminThemeOptions> admin
                    ? admin.Value.AdminPath
                    : "/";
            return Ok(new { returnUrl = Request.PathBase.Add(new Microsoft.AspNetCore.Http.PathString(adminPath)).Value });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    /// <summary>
    /// <c>ManageOrgMembers</c> asked against the organization or the binding (docs/access.md §
    /// Members, ruling 2026-10-13), so Members' mapper decides the reach: staff every
    /// organization, a caller acting in an organization only there.
    /// </summary>
    private Task<bool> CanManageAsync(object resource) =>
        decision.AllowsAsync(callerAccessor.Current, Permissions.MembersPermissions.ManageOrgMembers.Name, resource, HttpContext.RequestAborted);
}
