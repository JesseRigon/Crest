using Crest.AuditTrail.Models;

namespace Crest.AuditTrail.Services.Models;

public class AuditTrailEventQueryResult
{
    public IEnumerable<AuditTrailEvent> Events { get; set; }
    public int TotalCount { get; set; }
}
