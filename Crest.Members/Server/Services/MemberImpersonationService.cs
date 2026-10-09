using System.Security.Claims;
using Crest.Members.Constants;
using Crest.Members.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Crest.Entities;
using Crest.Users;
using Crest.Users.Models;
using Crest.Users.Services;
using YesSql;

namespace Crest.Members.Services;

/// <summary>
/// Staff support access to member portals (ruling: impersonation, never logging in as
/// the member; stock Crest has NO impersonation - verified, built from primitives).
/// The issued session IS the member's principal (functional attribution: CreatedBy =
/// member, member org scope, member view) while AuthenticationProperties carry the
/// STAFF identity, surfaced per request as the impersonator claim - both identities
/// ride the session, which is the dual-attribution prerequisite the audit layer will
/// consume ("staff X impersonating member Y"). Exiting re-issues the staff session.
/// </summary>
public class MemberImpersonationService
{
    private readonly YesSql.ISession _session;
    private readonly IUserService _userService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MemberImpersonationService(YesSql.ISession session, IUserService userService, IHttpContextAccessor httpContextAccessor)
    {
        _session = session;
        _userService = userService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Caller has already authorized ImpersonateMembers (staff-only via the
    /// ceiling). Refuses non-member targets and nested impersonation.</summary>
    public async Task StartAsync(string memberUserId)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var current = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (current.Principal is null)
        {
            throw new InvalidOperationException("No authenticated session.");
        }

        if (current.Properties?.Items.ContainsKey(MemberSessionKeys.ImpersonatorUserId) == true)
        {
            throw new InvalidOperationException("Already impersonating; stop the current impersonation first.");
        }

        var staffUserId = current.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("The current session carries no user id.");
        var staffUserName = current.Principal.Identity?.Name ?? staffUserId;

        var member = await _session.Query<User, Indexes.UserClassIndex>(index => index.UserId == memberUserId && index.Class == UserClasses.Member)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("The target user is not a member-class account.");

        var memberPrincipal = await _userService.CreatePrincipalAsync(member);

        var properties = new AuthenticationProperties();
        properties.Items[MemberSessionKeys.ImpersonatorUserId] = staffUserId;
        properties.Items[MemberSessionKeys.ImpersonatorUserName] = staffUserName;

        // Land in the member's first org by default; the org switcher changes it like
        // any member session.
        member.TryGet<CrestMemberInfo>(out var memberInfo);
        var firstOrg = memberInfo?.Bindings.FirstOrDefault()?.OrganizationId;
        if (firstOrg is not null)
        {
            properties.Items[MemberSessionKeys.ActiveOrganization] = firstOrg;
        }

        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, memberPrincipal, properties);
    }

    /// <summary>Ends the impersonation, restoring the recorded staff session. Throws
    /// when the current session is not an impersonation.</summary>
    public async Task StopAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var current = await httpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        if (current.Properties?.Items.TryGetValue(MemberSessionKeys.ImpersonatorUserId, out var staffUserId) != true || staffUserId is null)
        {
            throw new InvalidOperationException("The current session is not an impersonation.");
        }

        var staff = await _session.Query<User, Indexes.UserClassIndex>(index => index.UserId == staffUserId)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("The impersonating staff account no longer exists.");

        var staffPrincipal = await _userService.CreatePrincipalAsync(staff);
        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, staffPrincipal, new AuthenticationProperties());
    }
}
