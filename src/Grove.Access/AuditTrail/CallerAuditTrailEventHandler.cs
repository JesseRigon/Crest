using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.Entities;

namespace Grove.Access.AuditTrail;

/// <summary>
/// Completes every event's attribution from the caller the gate built (docs/audit.md › step 1):
/// the side, the organization, and what kind of actor it was. A machine acting without a user
/// is recorded with its client id as the actor (docs/machine-actors.md). Impersonation is
/// Members' handler's to resolve; this one only fills what the caller states.
/// </summary>
public sealed class CallerAuditTrailEventHandler(ICallerContextAccessor callers) : AuditTrailEventHandlerBase
{
    public override Task AlterAsync(AuditTrailCreateContext context, AuditTrailEvent auditTrailEvent)
    {
        var caller = callers.Current;
        if (caller is null)
        {
            return Task.CompletedTask;
        }

        if (caller.UserClass == CallerClasses.Application && string.IsNullOrEmpty(auditTrailEvent.UserId))
        {
            auditTrailEvent.UserId = caller.UserId;
            auditTrailEvent.UserName = caller.UserName ?? caller.UserId;
        }

        auditTrailEvent.Alter<AuditTrailAttribution>(attribution =>
        {
            attribution.Side ??= caller.Side.ToString();
            attribution.OrganizationId ??= caller.OrganizationId;

            if (caller.UserClass == CallerClasses.Application)
            {
                attribution.ActorKind = AuditTrailActorKinds.Application;
                attribution.ClientId ??= caller.UserId;
            }
            else if (caller.IsSystem && string.IsNullOrEmpty(auditTrailEvent.UserId))
            {
                attribution.ActorKind = AuditTrailActorKinds.System;
            }
        });

        return Task.CompletedTask;
    }
}
