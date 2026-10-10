using Crest.AuditTrail.Models;
using Crest.Entities;
using Crest.Users.AuditTrail.Models;
using Crest.Users.AuditTrail.Services;
using YesSql.Indexes;

namespace Crest.Users.AuditTrail.Indexes;

public class AuditTrailUserEventIndex : MapIndex
{
    public string UserId { get; set; }

    public bool HasUserSnapshot { get; set; }
}

public class AuditTrailUserEventIndexProvider : IndexProvider<AuditTrailEvent>
{
    public AuditTrailUserEventIndexProvider() => CollectionName = AuditTrailEvent.Collection;
    
    public override void Describe(DescribeContext<AuditTrailEvent> context) => context
        .For<AuditTrailUserEventIndex>()
        .When(auditTrailEvent =>
            auditTrailEvent.Category == UserAuditTrailEventConfiguration.CategoryName &&
            auditTrailEvent.Has<AuditTrailUserEvent>())
        .Map(auditTrailEvent =>
        {
            var userEvent = auditTrailEvent.GetOrCreate<AuditTrailUserEvent>();

            return new AuditTrailUserEventIndex
            {
                UserId = userEvent.UserId,
                HasUserSnapshot = userEvent.Snapshot != null,
            };
        });
}
