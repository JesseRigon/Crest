using Grove.AuditTrail.Models;
using Grove.AuditTrail.Services.Models;

namespace Grove.AuditTrail.ViewModels;

public class AuditTrailEventViewModel
{
    public AuditTrailEvent AuditTrailEvent { get; set; }
    public AuditTrailEventDescriptor Descriptor { get; set; }
}
