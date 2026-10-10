using Microsoft.AspNetCore.Mvc.ModelBinding;
using Crest.Rules.Models;

namespace Crest.Rules.ViewModels;

public class ContentTypeConditionViewModel
{
    public string SelectedOperation { get; set; }
    public string Value { get; set; }

    [BindNever]
    public ContentTypeCondition Condition { get; set; }
}
