using System.Security.Claims;
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

[ApiController]
[IgnoreAntiforgeryToken]
[Route("api/crest/members")]
public sealed class MembersController(
    IMemberService memberService,
    MemberSessionService sessionService,
    MemberImpersonationService impersonationService,
    UserClassConversionService conversionService,
    IAuthorizationService authorizationService) : ControllerBase
{
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MemberSessionModel>> MeAsync()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var member = userId is null ? null : await memberService.GetAsync(userId);
        var activeOrg = await sessionService.GetActiveOrganizationAsync(HttpContext);
        var impersonator = User.FindFirst(MemberClaims.Impersonator)?.Value;

        return Ok(new MemberSessionModel(member, activeOrg, impersonator));
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
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ManageMembers))
        {
            return Forbid();
        }

        return Ok(await memberService.ListByOrganizationAsync(organizationId, HttpContext.RequestAborted));
    }

    [HttpPost]
    public async Task<ActionResult<MemberModel>> CreateAsync(CreateMemberApiRequest request)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ManageMembers))
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

        var member = await memberService.GetAsync(((OrchardCore.Users.Models.User)created).UserId, HttpContext.RequestAborted);
        return Ok(member);
    }

    [HttpPost("{userId}/bindings")]
    public async Task<ActionResult<MemberOrgBindingModel>> AddBindingAsync(string userId, AddBindingRequest request)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ManageMembers))
        {
            return Forbid();
        }

        try
        {
            return Ok(await memberService.AddBindingAsync(userId, request.OrganizationId, request.Roles, HttpContext.RequestAborted));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpDelete("{userId}/bindings/{organizationId}")]
    public async Task<IActionResult> RemoveBindingAsync(string userId, string organizationId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ManageMembers))
        {
            return Forbid();
        }

        try
        {
            await memberService.RemoveBindingAsync(userId, organizationId, HttpContext.RequestAborted);
            return NoContent();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

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
            return Ok();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{userId}/convert-to-staff")]
    public async Task<IActionResult> ConvertToStaffAsync(string userId)
    {
        if (!await authorizationService.AuthorizeAsync(User, Permissions.MembersPermissions.ConvertUserClass))
        {
            return Forbid();
        }

        try
        {
            await conversionService.ConvertMemberToStaffAsync(userId, HttpContext.RequestAborted);
            return Ok();
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }
}
