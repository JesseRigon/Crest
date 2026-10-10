using Crest.DisplayManagement.Handlers;
using Crest.DisplayManagement.Views;
using Crest.Rules.Models;
using Crest.Rules.ViewModels;

namespace Crest.Rules.Drivers;

public sealed class AllConditionDisplayDriver : DisplayDriver<Condition, AllConditionGroup>
{
    public override Task<IDisplayResult> DisplayAsync(AllConditionGroup condition, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("AllCondition_Fields_Summary", condition).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("AllCondition_Fields_Thumbnail", condition).Location("Thumbnail", "Content"),
                Initialize<ConditionGroupViewModel>("ConditionGroup_Fields_Summary", m =>
                {
                    m.Entries = condition.Conditions.Select(x => new ConditionEntry { Condition = x }).ToArray();
                    m.Condition = condition;
                }).Location(PlatformConstants.DisplayType.Summary, "Content")
            );
    }

    public override IDisplayResult Edit(AllConditionGroup condition, BuildEditorContext context)
    {
        return Initialize<AllConditionViewModel>("AllCondition_Fields_Edit", m =>
        {
            m.DisplayText = condition.DisplayText;
            m.Condition = condition;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(AllConditionGroup condition, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(condition, Prefix, x => x.DisplayText);

        return Edit(condition, context);
    }
}
