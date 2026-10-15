using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Rules.Models;
using Grove.Rules.ViewModels;

namespace Grove.Rules.Drivers;

public sealed class HomepageConditionDisplayDriver : DisplayDriver<Condition, HomepageCondition>
{
    public override Task<IDisplayResult> DisplayAsync(HomepageCondition condition, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("HomepageCondition_Fields_Summary", condition).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("HomepageCondition_Fields_Thumbnail", condition).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(HomepageCondition condition, BuildEditorContext context)
    {
        return Initialize<HomepageConditionViewModel>("HomepageCondition_Fields_Edit", m =>
        {
            m.Value = condition.Value;
            m.Condition = condition;
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(HomepageCondition condition, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(condition, Prefix, x => x.Value);

        return Edit(condition, context);
    }
}
