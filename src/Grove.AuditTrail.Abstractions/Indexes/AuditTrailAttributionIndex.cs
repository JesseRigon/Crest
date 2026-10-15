using YesSql.Indexes;

namespace Grove.AuditTrail.Indexes;

/// <summary>
/// The dimensions audit views and activity feeds filter on, beside the stock
/// <see cref="AuditTrailEventIndex"/> (docs/audit.md › Build order: an index provider on the
/// stock Audit collection). One row per event.
/// </summary>
public class AuditTrailAttributionIndex : MapIndex
{
    public string EventId { get; set; }
    public string Category { get; set; }
    public string Name { get; set; }
    public DateTime CreatedUtc { get; set; }

    /// <summary>The audit attribution: who really acted.</summary>
    public string UserId { get; set; }

    public string ActorKind { get; set; }

    /// <summary>The functional attribution: who the record belongs to.</summary>
    public string FunctionalUserId { get; set; }

    public string ImpersonatedUserId { get; set; }
    public bool IsImpersonated { get; set; }
    public string SessionId { get; set; }
    public string OrganizationId { get; set; }
    public string Side { get; set; }

    public string RecordId { get; set; }
    public string RecordType { get; set; }
    public string OwnerId { get; set; }
}

/// <summary>The parties an event concerns, one row per party.</summary>
public class AuditTrailPartyIndex : MapIndex
{
    public string EventId { get; set; }
    public string PartyId { get; set; }
    public string Category { get; set; }
    public DateTime CreatedUtc { get; set; }
}
