using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services;
using Grove.AuditTrail.Services.Models;
using Grove.Contents.Management;
using Grove.Contents.AuditTrail.Models;
using Grove.Entities;

namespace Grove.Contents.AuditTrail.Services;

/// <summary>
/// Shapes content events: the editor's comment, the subject feeds filter on (the item, its
/// type, its owner), and the stored form - a diff with checkpoints instead of a snapshot
/// (<see cref="ContentHistory"/>), applied only to events that are actually saved.
/// </summary>
public class ContentAuditTrailEventHandler(ContentHistory history) : AuditTrailEventHandlerBase
{
    public override Task CreateAsync(AuditTrailCreateContext context)
    {
        if (context.Category != ContentAuditTrailEventConfiguration.Content
            || context is not AuditTrailCreateContext<AuditTrailContentEvent> contentEvent
            || contentEvent.AuditTrailEventItem.ContentItem is not { } contentItem)
        {
            return Task.CompletedTask;
        }

        if (contentItem.TryGet<AuditTrailFieldSet>(out var auditTrailFieldSet))
        {
            contentEvent.AuditTrailEventItem.Comment = auditTrailFieldSet.Comment;
        }

        context.Subject ??= new AuditTrailSubject();
        context.Subject.RecordId ??= contentItem.ContentItemId;
        context.Subject.RecordType ??= contentItem.ContentType;
        context.Subject.DisplayText ??= contentItem.DisplayText;
        context.Subject.OwnerId ??= contentItem.Owner;

        return Task.CompletedTask;
    }

    public override async Task AlterAsync(AuditTrailCreateContext context, AuditTrailEvent auditTrailEvent)
    {
        if (context.Category != ContentAuditTrailEventConfiguration.Content
            || context is not AuditTrailCreateContext<AuditTrailContentEvent> contentContext
            || contentContext.AuditTrailEventItem.ContentItem is null)
        {
            return;
        }

        var contentEvent = contentContext.AuditTrailEventItem;
        await history.PrepareAsync(auditTrailEvent, contentEvent, context.Name == ContentAuditTrailEventConfiguration.Published);
        auditTrailEvent.Put(contentEvent);
    }
}
