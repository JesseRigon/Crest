using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Rules.Models;

namespace Crest.Rules.ViewModels;

public class HomepageConditionViewModel
{
    public bool Value { get; set; }

    [BindNever]
    public HomepageCondition Condition { get; set; }
}
