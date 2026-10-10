using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Rules.Models;

namespace Crest.Rules.ViewModels;

public class AnyConditionViewModel
{
    public string DisplayText { get; set; }

    [BindNever]
    public AnyConditionGroup Condition { get; set; }
}
