using Microsoft.AspNetCore.Mvc.ModelBinding;
using Grove.Rules.Models;

namespace Grove.Rules.ViewModels;

public class JavascriptConditionViewModel
{
    public string Script { get; set; }

    [BindNever]
    public JavascriptCondition Condition { get; set; }
}
