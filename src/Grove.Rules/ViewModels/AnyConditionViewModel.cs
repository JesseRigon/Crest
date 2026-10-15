using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Rules.Models;

namespace Grove.Rules.ViewModels;

public class AnyConditionViewModel
{
    public string DisplayText { get; set; }

    [BindNever]
    public AnyConditionGroup Condition { get; set; }
}
