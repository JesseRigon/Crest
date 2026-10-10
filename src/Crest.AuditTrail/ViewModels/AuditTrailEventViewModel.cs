using Crest.AuditTrail.Models;
using Crest.AuditTrail.Services.Models;

namespace Crest.AuditTrail.ViewModels;

public class AuditTrailEventViewModel
{
    public AuditTrailEvent AuditTrailEvent { get; set; }
    public AuditTrailEventDescriptor Descriptor { get; set; }
}
