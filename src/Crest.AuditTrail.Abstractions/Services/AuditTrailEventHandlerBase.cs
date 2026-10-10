using Crest.AuditTrail.Models;
using Crest.AuditTrail.Services.Models;

namespace Crest.AuditTrail.Services;

public class AuditTrailEventHandlerBase : IAuditTrailEventHandler
{
    public virtual Task CreateAsync(AuditTrailCreateContext context) => Task.CompletedTask;
    public virtual Task AlterAsync(AuditTrailCreateContext context, AuditTrailEvent auditTrailEvent) => Task.CompletedTask;
}
