using Grove.AuditTrail.Models;
using Grove.Contents.AuditTrail.Models;
using Grove.Contents.AuditTrail.Services;
using Grove.Entities;
using YesSql.Indexes;

namespace Grove.Contents.AuditTrail.Indexes;

/// <summary>A content item's history: where each event sits and which events hold a full version.</summary>
public class ContentHistoryIndex : MapIndex
{
    public string EventId { get; set; }

    public string ContentItemId { get; set; }

    public int Sequence { get; set; }

    /// <summary>The event holds the full item (a checkpoint or the published snapshot), so a rebuild can start from it.</summary>
    public bool HasFullState { get; set; }

    /// <summary>The event holds the current published version uncompressed.</summary>
    public bool IsPublishedSnapshot { get; set; }

    public DateTime CreatedUtc { get; set; }
}

public class ContentHistoryIndexProvider : IndexProvider<AuditTrailEvent>
{
    public ContentHistoryIndexProvider() => CollectionName = AuditTrailEvent.Collection;

    public override void Describe(DescribeContext<AuditTrailEvent> context) => context
        .For<ContentHistoryIndex>()
        .When(auditTrailEvent => auditTrailEvent.Category == ContentAuditTrailEventConfiguration.Content && auditTrailEvent.Has<AuditTrailContentEvent>())
        .Map(auditTrailEvent =>
        {
            var contentEvent = auditTrailEvent.As<AuditTrailContentEvent>();
            if (contentEvent.Sequence <= 0)
            {
                return null;
            }

            return new ContentHistoryIndex
            {
                EventId = auditTrailEvent.EventId,
                ContentItemId = contentEvent.ContentItemId,
                Sequence = contentEvent.Sequence,
                HasFullState = contentEvent.ContentItem is not null || !string.IsNullOrEmpty(contentEvent.Checkpoint),
                IsPublishedSnapshot = contentEvent.ContentItem is not null,
                CreatedUtc = auditTrailEvent.CreatedUtc,
            };
        });
}
