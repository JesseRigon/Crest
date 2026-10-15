using Grove.DisplayManagement.Handlers;
using Grove.DisplayManagement.Views;
using Grove.Rules.Models;

namespace Grove.Rules.Drivers;

public sealed class IsAnonymousConditionDisplayDriver : DisplayDriver<Condition, IsAnonymousCondition>
{
    public override Task<IDisplayResult> DisplayAsync(IsAnonymousCondition condition, BuildDisplayContext context)
    {
        return
            CombineAsync(
                View("IsAnonymousCondition_Fields_Summary", condition).Location(PlatformConstants.DisplayType.Summary, "Content"),
                View("IsAnonymousCondition_Fields_Thumbnail", condition).Location("Thumbnail", "Content")
            );
    }

    public override IDisplayResult Edit(IsAnonymousCondition condition, BuildEditorContext context)
    {
        return View("IsAnonymousCondition_Fields_Edit", condition).Location("Content");
    }
}
