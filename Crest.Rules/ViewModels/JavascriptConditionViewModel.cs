using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Rules.Models;

namespace Crest.Rules.ViewModels;

public class JavascriptConditionViewModel
{
    public string Script { get; set; }

    [BindNever]
    public JavascriptCondition Condition { get; set; }
}
