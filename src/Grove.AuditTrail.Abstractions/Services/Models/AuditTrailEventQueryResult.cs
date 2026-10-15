using Grove.AuditTrail.Models;

namespace Grove.AuditTrail.Services.Models;

public class AuditTrailEventQueryResult
{
    public IEnumerable<AuditTrailEvent> Events { get; set; }
    public int TotalCount { get; set; }
}
