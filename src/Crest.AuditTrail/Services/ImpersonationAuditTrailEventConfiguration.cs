using Crest.AuditTrail.Models;
using Crest.AuditTrail.Services.Models;
using Microsoft.Extensions.Options;

namespace Crest.AuditTrail.Services;

/// <summary>
/// Impersonation start and stop (docs/audit.md › step 2): recorded by whichever module issues
/// impersonated sessions (Crest.Members), always, since Orchard has no impersonation of its own.
/// </summary>
public sealed class ImpersonationAuditTrailEventConfiguration : IConfigureOptions<AuditTrailOptions>
{
    public void Configure(AuditTrailOptions options)
    {
        options.For<ImpersonationAuditTrailEventConfiguration>(AuditTrailSessionEvents.Category, S => S["Impersonation"])
            .WithEvent(AuditTrailSessionEvents.Started, S => S["Started"], S => S["A staff user started impersonating a member."], true, isMandatory: true)
            .WithEvent(AuditTrailSessionEvents.Stopped, S => S["Stopped"], S => S["A staff user stopped impersonating a member."], true, isMandatory: true);
    }
}
