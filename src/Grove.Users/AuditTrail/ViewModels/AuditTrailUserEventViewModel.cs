using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.AuditTrail.Models;
using Grove.Users.AuditTrail.Models;

namespace Grove.Users.AuditTrail.ViewModels;

public class AuditTrailUserEventViewModel
{
    [BindNever]
    public AuditTrailUserEvent UserEvent { get; set; }

    [BindNever]
    public AuditTrailEvent AuditTrailEvent { get; set; }
}
