using System.Security.Claims;
using Crest.AuditTrail.Models;
using Crest.Members.AuditTrail;
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
    private readonly MemberAuditRecorder _audit;

    public MemberImpersonationService(YesSql.ISession session, IUserService userService, IHttpContextAccessor httpContextAccessor, MemberAuditRecorder audit)
    {
        _session = session;
        _userService = userService;
        _httpContextAccessor = httpContextAccessor;
        _audit = audit;
    }

    /// <summary>Caller has already authorized ImpersonateMembers (staff-only via the
    /// ceiling). Refuses non-member targets and nested impersonation.</summary>
    public async Task StartAsync(string memberUserId)
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var current = MemberSessionService.Gated(httpContext);
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

        var member = await _session.Query<User, Indexes.UserConnectionIndex>(index => index.UserId == memberUserId && index.Class == UserClasses.Member)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("The target user is not a member-class account.");

        var memberPrincipal = await _userService.CreatePrincipalAsync(member);

        var sessionId = IdGenerator.GenerateId();
        var properties = new AuthenticationProperties();
        properties.Items[MemberSessionKeys.ImpersonatorUserId] = staffUserId;
        properties.Items[MemberSessionKeys.ImpersonatorUserName] = staffUserName;
        properties.Items[MemberSessionKeys.ImpersonationSessionId] = sessionId;

        // The staff session's own state, so stopping restores it exactly: its persistence and,
        // for a dual user, the organization it last acted in on the member side.
        properties.IsPersistent = current.Properties?.IsPersistent ?? false;
        properties.Items[MemberSessionKeys.ImpersonatorPersistent] = properties.IsPersistent ? "true" : "false";
        if (current.Properties?.Items.TryGetValue(MemberSessionKeys.ActiveOrganization, out var staffOrganization) == true
            && !string.IsNullOrWhiteSpace(staffOrganization))
        {
            properties.Items[MemberSessionKeys.ImpersonatorActiveOrganization] = staffOrganization;
        }

        // Land in the member's first org by default; the org switcher changes it like
        // any member session. A dual user can be impersonated: the session acts on the member
        // side only (MemberCallerContributor), so their staff grants never apply.
        member.TryGet<CrestMemberInfo>(out var memberInfo);
        var firstOrg = memberInfo?.Bindings.FirstOrDefault(binding => binding.Class == UserClasses.Member)?.OrganizationId;
        if (firstOrg is not null)
        {
            properties.Items[MemberSessionKeys.ActiveOrganization] = firstOrg;
        }

        // Recorded while the session is still the staff user's: the start names the member,
        // the organization and the session explicitly.
        await _audit.RecordAsync(
            AuditTrailSessionEvents.Category,
            AuditTrailSessionEvents.Started,
            sessionId,
            new MemberAuditTrailEvent { MemberUserId = member.UserId, MemberUserName = member.UserName, OrganizationId = firstOrg },
            new AuditTrailSubject { RecordId = member.UserId, RecordType = "User", DisplayText = member.UserName },
            new AuditTrailAttribution
            {
                ImpersonatedUserId = member.UserId,
                ImpersonatedUserName = member.UserName,
                FunctionalUserId = member.UserId,
                FunctionalUserName = member.UserName,
                OrganizationId = firstOrg,
                SessionId = sessionId,
            },
            staffUserId,
            staffUserName);

        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, memberPrincipal, properties);
    }

    /// <summary>Ends the impersonation, restoring the recorded staff session. Throws
    /// when the current session is not an impersonation.</summary>
    public async Task StopAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext!;
        var current = MemberSessionService.Gated(httpContext);

        if (current.Properties?.Items.TryGetValue(MemberSessionKeys.ImpersonatorUserId, out var staffUserId) != true || staffUserId is null)
        {
            throw new InvalidOperationException("The current session is not an impersonation.");
        }

        var staff = await _session.Query<User, Crest.Users.Indexes.UserIndex>(index => index.UserId == staffUserId)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("The impersonating staff account no longer exists.");

        // Recorded inside the impersonated session, so the actor handler attributes it to the
        // staff user impersonating the member, in the session's organization and session.
        current.Properties!.Items.TryGetValue(MemberSessionKeys.ImpersonationSessionId, out var sessionId);
        current.Properties!.Items.TryGetValue(MemberSessionKeys.ActiveOrganization, out var organizationId);
        var memberUserId = current.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var memberUserName = current.Principal?.Identity?.Name;
        await _audit.RecordAsync(
            AuditTrailSessionEvents.Category,
            AuditTrailSessionEvents.Stopped,
            sessionId ?? string.Empty,
            new MemberAuditTrailEvent { MemberUserId = memberUserId, MemberUserName = memberUserName, OrganizationId = organizationId },
            new AuditTrailSubject { RecordId = memberUserId, RecordType = "User", DisplayText = memberUserName },
            userId: memberUserId,
            userName: memberUserName);

        // Restores the staff session exactly as it was before the impersonation started: its
        // persistence and its own last active organization (a dual user's), never the member's.
        var restored = new AuthenticationProperties
        {
            IsPersistent = current.Properties!.Items.TryGetValue(MemberSessionKeys.ImpersonatorPersistent, out var persistent)
                && string.Equals(persistent, "true", StringComparison.Ordinal),
        };
        if (current.Properties.Items.TryGetValue(MemberSessionKeys.ImpersonatorActiveOrganization, out var staffOrganization)
            && !string.IsNullOrWhiteSpace(staffOrganization))
        {
            restored.Items[MemberSessionKeys.ActiveOrganization] = staffOrganization;
        }

        var staffPrincipal = await _userService.CreatePrincipalAsync(staff);
        await httpContext.SignInAsync(IdentityConstants.ApplicationScheme, staffPrincipal, restored);
    }
}
