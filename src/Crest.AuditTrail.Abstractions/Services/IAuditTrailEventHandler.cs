using Crest.AuditTrail.Models;
using Crest.AuditTrail.Services.Models;

namespace Crest.AuditTrail.Services;

/// <summary>
/// Describes an event handler participating in Audit Trail management.
/// </summary>
public interface IAuditTrailEventHandler
{
    /// <summary>
    /// Modifies the context when creating an Audit Trail event.
    /// </summary>
    Task CreateAsync(AuditTrailCreateContext context);

    /// <summary>
    /// Alters an Audit Trail event after it has been built and before saving it.
    /// </summary>
    Task AlterAsync(AuditTrailCreateContext context, AuditTrailEvent auditTrailEvent);
}
