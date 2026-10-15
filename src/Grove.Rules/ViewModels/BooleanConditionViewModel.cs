using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Rules.Models;

namespace Grove.Rules.ViewModels;

public class BooleanConditionViewModel
{
    public bool Value { get; set; }

    [BindNever]
    public BooleanCondition Condition { get; set; }
}
