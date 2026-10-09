using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Crest.Rules.ViewModels;

public class ConditionGroupViewModel
{
    public ConditionEntry[] Entries { get; set; }

    [BindNever]
    public ConditionGroup Condition { get; set; }
}

public class ConditionEntry
{
    [BindNever]
    public Condition Condition { get; set; }
}
