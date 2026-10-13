using Crest.AuditTrail.Models;
using Crest.Entities;
using YesSql.Indexes;

namespace Crest.AuditTrail.Indexes;

/// <summary>
/// Indexes both attributions and the subject of every event (docs/audit.md › step 5: events
/// carry what feeds filter on, indexed), and the parties an event concerns.
/// </summary>
public class AuditTrailAttributionIndexProvider : IndexProvider<AuditTrailEvent>
{
    public AuditTrailAttributionIndexProvider() => CollectionName = AuditTrailEvent.Collection;

    public override void Describe(DescribeContext<AuditTrailEvent> context)
    {
        context.For<AuditTrailAttributionIndex>()
            .Map(auditTrailEvent =>
            {
                var attribution = auditTrailEvent.As<AuditTrailAttribution>() ?? new AuditTrailAttribution();
                var subject = auditTrailEvent.As<AuditTrailSubject>() ?? new AuditTrailSubject();

                return new AuditTrailAttributionIndex
                {
                    EventId = auditTrailEvent.EventId,
                    Category = auditTrailEvent.Category,
                    Name = auditTrailEvent.Name,
                    CreatedUtc = auditTrailEvent.CreatedUtc,
                    UserId = auditTrailEvent.UserId,
                    ActorKind = attribution.ActorKind,
                    FunctionalUserId = attribution.FunctionalUserId,
                    ImpersonatedUserId = attribution.ImpersonatedUserId,
                    IsImpersonated = attribution.ImpersonatedUserId is not null,
                    SessionId = attribution.SessionId,
                    OrganizationId = attribution.OrganizationId,
                    Side = attribution.Side,
                    RecordId = subject.RecordId ?? auditTrailEvent.CorrelationId,
                    RecordType = subject.RecordType,
                    OwnerId = subject.OwnerId,
                };
            });

        context.For<AuditTrailPartyIndex>()
            .Map(auditTrailEvent =>
            {
                var subject = auditTrailEvent.As<AuditTrailSubject>();
                if (subject?.PartyIds is not { Count: > 0 } partyIds)
                {
                    return [];
                }

                return partyIds
                    .Where(partyId => !string.IsNullOrEmpty(partyId))
                    .Distinct(StringComparer.Ordinal)
                    .Select(partyId => new AuditTrailPartyIndex
                    {
                        EventId = auditTrailEvent.EventId,
                        PartyId = partyId,
                        Category = auditTrailEvent.Category,
                        CreatedUtc = auditTrailEvent.CreatedUtc,
                    });
            });
    }
}
