using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Rules.Models;

namespace Grove.Rules.ViewModels;

public class HomepageConditionViewModel
{
    public bool Value { get; set; }

    [BindNever]
    public HomepageCondition Condition { get; set; }
}
