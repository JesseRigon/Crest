using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.DisplayManagement;

namespace Grove.AuditTrail.ViewModels;

public class AuditTrailItemViewModel
{
    [BindNever]
    public IShape Shape { get; set; }
}
