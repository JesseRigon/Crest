using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Rules.ViewModels;

namespace Crest.Rules.Drivers;

public sealed class RuleDisplayDriver : DisplayDriver<Rule>
{
    public override Task<IDisplayResult> DisplayAsync(Rule rule, BuildDisplayContext context)
    {
        return CombineAsync(
            View("Rule_Fields_Summary", rule).Location(PlatformConstants.DisplayType.Summary, "Content"),
            Initialize<ConditionGroupViewModel>("ConditionGroup_Fields_Summary", m =>
            {
                m.Entries = rule.Conditions.Select(x => new ConditionEntry { Condition = x }).ToArray();
                m.Condition = rule;
            }).Location(PlatformConstants.DisplayType.Summary, "Content")
        );
    }
}
