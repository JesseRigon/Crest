using Grove.Access;
using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.Entities;
using Grove.Members.Constants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Grove.Members.AuditTrail;

/// <summary>
/// The one actor handler (docs/audit.md › step 1): runs for every event, stock and Grove. When
/// the session is an impersonation it moves the event's user to the staff user and stores the
/// impersonated member, the active organization and the session on the event, so no write path
/// needs to know about impersonation. Stock attribution takes the user from the request
/// principal, which during impersonation is the member: that becomes the functional
/// attribution.
/// </summary>
public sealed class ImpersonationAuditTrailEventHandler(
    IHttpContextAccessor httpContextAccessor,
    ICallerContextAccessor callers) : AuditTrailEventHandlerBase
{
    public override Task AlterAsync(AuditTrailCreateContext context, AuditTrailEvent auditTrailEvent)
    {
        var caller = callers.Current;
        var items = SessionItems(httpContextAccessor.HttpContext);

        var staffUserId = caller?.ImpersonatorUserId ?? Item(items, MemberSessionKeys.ImpersonatorUserId);
        if (string.IsNullOrEmpty(staffUserId))
        {
            return Task.CompletedTask;
        }

        // The member the session acts as: the caller, or the principal the event was taken from.
        var memberUserId = caller?.UserId ?? auditTrailEvent.UserId;
        var memberUserName = caller?.UserName ?? auditTrailEvent.UserName;
        var staffUserName = Item(items, MemberSessionKeys.ImpersonatorUserName) ?? staffUserId;
        var sessionId = Item(items, MemberSessionKeys.ImpersonationSessionId);
        var organizationId = caller?.OrganizationId ?? Item(items, MemberSessionKeys.ActiveOrganization);

        // An event a recorder attributed to someone else entirely (a system write inside the
        // request) keeps its user; only the session's own identity is the staff user's.
        if (!string.IsNullOrEmpty(auditTrailEvent.UserId) && auditTrailEvent.UserId != memberUserId && auditTrailEvent.UserId != staffUserId)
        {
            return Task.CompletedTask;
        }

        auditTrailEvent.UserId = staffUserId;
        auditTrailEvent.UserName = staffUserName;
        auditTrailEvent.Alter<AuditTrailAttribution>(attribution =>
        {
            attribution.ActorKind = AuditTrailActorKinds.User;
            attribution.ImpersonatedUserId ??= memberUserId;
            attribution.ImpersonatedUserName ??= memberUserName;
            attribution.FunctionalUserId = attribution.ImpersonatedUserId;
            attribution.FunctionalUserName = attribution.ImpersonatedUserName;
            attribution.OrganizationId ??= organizationId;
            attribution.SessionId ??= sessionId;
        });

        return Task.CompletedTask;
    }

    private static IDictionary<string, string?>? SessionItems(HttpContext? httpContext)
        => httpContext?.Features.Get<IAuthenticateResultFeature>()?.AuthenticateResult?.Properties?.Items;

    private static string? Item(IDictionary<string, string?>? items, string key)
        => items is not null && items.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value) ? value : null;
}
