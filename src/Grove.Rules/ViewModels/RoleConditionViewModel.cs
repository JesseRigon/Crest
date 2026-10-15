using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Rules.Models;

namespace Grove.Rules.ViewModels;

public class RoleConditionViewModel
{
    public string SelectedOperation { get; set; }
    public string Value { get; set; }

    [BindNever]
    public RoleCondition Condition { get; set; }
}
