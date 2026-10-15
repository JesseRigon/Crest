using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Rules.Models;

namespace Grove.Rules.ViewModels;

public class AllConditionViewModel
{
    public string DisplayText { get; set; }

    [BindNever]
    public AllConditionGroup Condition { get; set; }
}
