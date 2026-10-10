using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Rules.Models;

namespace Crest.Rules.ViewModels;

public class AllConditionViewModel
{
    public string DisplayText { get; set; }

    [BindNever]
    public AllConditionGroup Condition { get; set; }
}
