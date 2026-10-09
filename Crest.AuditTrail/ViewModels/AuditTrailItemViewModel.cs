using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.DisplayManagement;

namespace Crest.AuditTrail.ViewModels;

public class AuditTrailItemViewModel
{
    [BindNever]
    public IShape Shape { get; set; }
}
