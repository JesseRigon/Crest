using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Rules.Models;

namespace Crest.Rules.ViewModels;

public class BooleanConditionViewModel
{
    public bool Value { get; set; }

    [BindNever]
    public BooleanCondition Condition { get; set; }
}
