using Crest.Settings;
using YesSql;
using Crest.Entities;
using System.Security.Claims;
using Crest.Access;
using Crest.Members.Constants;
using Crest.Members.Models;
using Crest.Members.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crest.Members.Controllers;

public sealed record CreateMemberApiRequest(string UserName, string? Email, string? Password, string? PersonId, string OrganizationId, IReadOnlyList<string>? Roles);
public sealed record AddBindingRequest(string OrganizationId, IReadOnlyList<string>? Roles);
public sealed record SetActiveOrgRequest(string OrganizationId);
public sealed record ImpersonateRequest(string UserId);
public sealed record HierarchyEdgeRequest(string ReportId, string ManagerId);

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/members")]
public sealed class MembersController(
    IMemberService memberService,
    MemberBindingService bindings,
    UserClassService userClasses,
    IUserHierarchyService hierarchy,
    Microsoft.AspNetCore.Identity.UserManager<Crest.Users.IUser> userManager,
    Crest.Settings.ISiteService siteService,
    MemberSessionService sessionService,
    MemberOrganizationDirectory organizationDirectory,
    MemberImpersonationService impersonationService,
    IAuthorizationService authorizationService,
    Crest.Access.IAccessDecision decision,
    Crest.Access.ICallerContextAccessor callerAccessor) : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MemberSessionModel>> MeAsync()
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
    [HttpPost("active-org")]
    [Authorize]
    public async Task<IActionResult> SetActiveOrgAsync(SetActiveOrgRequest request)
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

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberModel>>> ListAsync([FromQuery] string organizationId)
    {
        if (!await CanManageAsync(new MemberOrganizationResource(organizationId)))
        {
            return Forbid();
        }

        return Ok(await memberService.ListByOrganizationAsync(organizationId, HttpContext.RequestAborted));
    }

    [HttpPost]
    public async Task<ActionResult<MemberModel>> CreateAsync(CreateMemberApiRequest request)
    {
        if (!await CanManageAsync(new MemberOrganizationResource(request.OrganizationId)))
        {
            return Forbid();
        }

        var created = await memberService.CreateMemberAsync(
            new CreateMemberRequest(request.UserName, request.Email, request.Password, request.PersonId, request.OrganizationId, request.Roles),
            (key, message) => ModelState.AddModelError(key, message),
            HttpContext.RequestAborted);

        if (created is null)
        {
            return ValidationProblem(ModelState);
        }

        var member = await memberService.GetAsync(((Crest.Users.Models.User)created).UserId, HttpContext.RequestAborted);
        return Ok(member);
    }

    [HttpPost("{userId}/bindings")]
    public async Task<ActionResult<MemberOrgBindingModel>> AddBindingAsync(string userId, AddBindingRequest request)
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

    [HttpDelete("{userId}/bindings/{organizationId}")]
    public async Task<IActionResult> RemoveBindingAsync(string userId, string organizationId)
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
    [HttpGet("{userId}/classes")]
    public async Task<ActionResult<UserConnectionsModel>> GetClassesAsync(string userId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ConvertUserClass))
        {
            return Forbid();
        }

        return await userClasses.GetAsync(userId, HttpContext.RequestAborted) is { } model ? Ok(model) : NotFound();
    }

    [HttpPost("{userId}/classes")]
    public async Task<ActionResult<UserConnectionsModel>> AddClassAsync(string userId, AddUserClassRequest request)
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

    [HttpDelete("{userId}/classes/{userClass}")]
    public async Task<ActionResult<UserConnectionsModel>> RemoveClassAsync(string userId, string userClass)
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
    [HttpGet("settings")]
    public async Task<ActionResult<MembersSettingsModel>> GetSettingsAsync()
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ManageMembers))
        {
            return Forbid();
        }

        var settings = await siteService.GetSettingsAsync<MembersSettings>();
        return Ok(new MembersSettingsModel(settings.AllowMembersOnTenantOrganization));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<MembersSettingsModel>> SetSettingsAsync(MembersSettingsModel request)
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
    [HttpPost("rebuild")]
    public async Task<IActionResult> RebuildAsync()
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
    [HttpGet("hierarchy/{organizationId}")]
    public async Task<ActionResult<IReadOnlyList<HierarchyNodeModel>>> GetHierarchyAsync(string organizationId)
    {
        if (!await CanManageAsync(new MemberOrganizationResource(organizationId)))
        {
            return Forbid();
        }

        return Ok(await hierarchy.GetNodesAsync(organizationId, HttpContext.RequestAborted));
    }

    [HttpPost("hierarchy/{organizationId}/edges")]
    public async Task<IActionResult> AddHierarchyEdgeAsync(string organizationId, HierarchyEdgeRequest request)
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

    [HttpDelete("hierarchy/{organizationId}/edges/{reportId}/{managerId}")]
    public async Task<IActionResult> RemoveHierarchyEdgeAsync(string organizationId, string reportId, string managerId)
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

    // Through the user store (the request's unit of work), not a session query of its own.
    private async Task<Crest.Users.Models.User> RequireUserAsync(string userId) =>
        await userManager.FindByIdAsync(userId) as Crest.Users.Models.User
            ?? throw new InvalidOperationException("Unknown user.");

    [HttpPost("impersonate")]
    public async Task<IActionResult> ImpersonateAsync(ImpersonateRequest request)
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
    [HttpPost("impersonate/stop")]
    [Authorize]
    public async Task<IActionResult> StopImpersonationAsync()
    {
        try
        {
            await impersonationService.StopAsync();
            // Back to the admin, where the restored staff session acts.
            var adminPath = HttpContext.RequestServices.GetService(typeof(Microsoft.Extensions.Options.IOptions<Crest.Middlewares.BlazorAdminThemeOptions>))
                is Microsoft.Extensions.Options.IOptions<Crest.Middlewares.BlazorAdminThemeOptions> admin
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
