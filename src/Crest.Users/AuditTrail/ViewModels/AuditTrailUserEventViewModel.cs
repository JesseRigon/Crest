using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.AuditTrail.Models;
using Crest.Users.AuditTrail.Models;

namespace Crest.Users.AuditTrail.ViewModels;

public class AuditTrailUserEventViewModel
{
    [BindNever]
    public AuditTrailUserEvent UserEvent { get; set; }

    [BindNever]
    public AuditTrailEvent AuditTrailEvent { get; set; }
}
