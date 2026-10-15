using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services.Models;

namespace Grove.AuditTrail.Services;

public class AuditTrailEventHandlerBase : IAuditTrailEventHandler
{
    public virtual Task CreateAsync(AuditTrailCreateContext context) => Task.CompletedTask;
    public virtual Task AlterAsync(AuditTrailCreateContext context, AuditTrailEvent auditTrailEvent) => Task.CompletedTask;
}
