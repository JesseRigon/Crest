using Crest.AuditTrail.Services;
using Crest.AuditTrail.Services.Models;
using Crest.ContentManagement;
using Crest.Contents.AuditTrail.Models;
using Crest.Entities;

namespace Crest.Contents.AuditTrail.Services;

public class ContentAuditTrailEventHandler : AuditTrailEventHandlerBase
{
    public override Task CreateAsync(AuditTrailCreateContext context)
    {
        if (context.Category != "Content")
        {
            return Task.CompletedTask;
        }

        if (context is AuditTrailCreateContext<AuditTrailContentEvent> contentEvent)
        {
            if (!contentEvent.AuditTrailEventItem.ContentItem.TryGet<AuditTrailPart>(out var auditTrailPart))
            {
                return Task.CompletedTask;
            }

            contentEvent.AuditTrailEventItem.Comment = auditTrailPart.Comment;
        }

        return Task.CompletedTask;
    }
}
