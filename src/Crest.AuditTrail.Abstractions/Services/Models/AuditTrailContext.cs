namespace Crest.AuditTrail.Services.Models;

public abstract class AuditTrailContext
{
    public AuditTrailContext(
        string name,
        string category,
        string correlationId,
        string userId,
        string userName)
    {
        Name = name;
        Category = category;
        CorrelationId = correlationId;
        UserId = userId;
        UserName = userName;
    }

    public string Name { get; set; }
    public string Category { get; set; }
    public string CorrelationId { get; set; }
    public string UserId { get; set; }
    public string UserName { get; set; }

    /// <summary>What the event is about (docs/audit.md › step 5); stored on the event and indexed.</summary>
    public Crest.AuditTrail.Models.AuditTrailSubject Subject { get; set; }

    /// <summary>
    /// Attribution the recorder already knows (an impersonation start names the member before the
    /// session carries it). The actor handlers complete it; what is set here is kept.
    /// </summary>
    public Crest.AuditTrail.Models.AuditTrailAttribution Attribution { get; set; }
}

public class AuditTrailContext<TEvent> : AuditTrailContext where TEvent : class, new()
{
    public AuditTrailContext(
        string name,
        string category,
        string correlationId,
        string userId,
        string userName,
        TEvent auditTrailEventItem)
        : base(name, category, correlationId, userId, userName)
    {
        AuditTrailEventItem = auditTrailEventItem;
    }

    public TEvent AuditTrailEventItem { get; }
}
